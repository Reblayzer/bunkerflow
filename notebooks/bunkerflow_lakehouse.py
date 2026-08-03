# Databricks notebook source
# MAGIC %md
# MAGIC # BunkerFlow on Databricks
# MAGIC
# MAGIC Picks up where the gateway leaves off. BunkerFlow lands normalized trade
# MAGIC events as date-partitioned Parquet; this notebook turns that output into
# MAGIC Delta tables and aggregates it.
# MAGIC
# MAGIC It reads real output committed to the public repo under `samples/landing/`,
# MAGIC so it runs on a fresh Databricks Free Edition workspace with no setup
# MAGIC beyond signing in.
# MAGIC
# MAGIC | Layer | What it is |
# MAGIC | --- | --- |
# MAGIC | Bronze | The landed events exactly as the gateway wrote them, plus the `dt` partition |
# MAGIC | Silver | Column names standardized, types tightened, deduplicated on event id, schema version enforced |
# MAGIC | Gold | Volume and value per port and fuel grade |

# COMMAND ----------

CATALOG = "workspace"
SCHEMA = "bunkerflow"
VOLUME = "landing"
PARTITION = "dt=2026-08-01"

GITHUB_RAW = (
    "https://raw.githubusercontent.com/Reblayzer/bunkerflow/main/samples/landing"
)

# The two files a single compose run produced: one batch flush, one follow-up.
FILES = [
    "events-20260802132325365.parquet",
    "events-20260802132340358.parquet",
]

VOLUME_ROOT = f"/Volumes/{CATALOG}/{SCHEMA}/{VOLUME}"

# COMMAND ----------

spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.{SCHEMA}")
spark.sql(f"CREATE VOLUME IF NOT EXISTS {CATALOG}.{SCHEMA}.{VOLUME}")

print(f"ready: {CATALOG}.{SCHEMA}, volume at {VOLUME_ROOT}")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Pull the landed Parquet into a volume
# MAGIC
# MAGIC If your workspace blocks outbound internet, skip this cell and upload the
# MAGIC two files from `samples/landing/dt=2026-08-01/` by hand:
# MAGIC **Catalog → workspace → bunkerflow → landing → Upload to this volume**,
# MAGIC into a `dt=2026-08-01` folder.

# COMMAND ----------

import os
import urllib.request

target_dir = f"{VOLUME_ROOT}/{PARTITION}"
os.makedirs(target_dir, exist_ok=True)

for file_name in FILES:
    destination = f"{target_dir}/{file_name}"
    if os.path.exists(destination):
        print(f"already present: {file_name}")
        continue

    urllib.request.urlretrieve(f"{GITHUB_RAW}/{PARTITION}/{file_name}", destination)
    print(f"downloaded: {file_name} ({os.path.getsize(destination)} bytes)")

display(dbutils.fs.ls(target_dir))

# COMMAND ----------

# MAGIC %md
# MAGIC ## Bronze
# MAGIC
# MAGIC Read the partition root rather than the files, so Spark picks `dt` up as a
# MAGIC partition column. That is the same Hive-style layout the gateway writes and
# MAGIC a production lakehouse expects.

# COMMAND ----------

bronze = spark.read.parquet(VOLUME_ROOT)

print(f"{bronze.count()} landed events")
bronze.printSchema()
display(bronze.limit(5))

# COMMAND ----------

bronze.write.mode("overwrite").saveAsTable(f"{CATALOG}.{SCHEMA}.bronze_landed_events")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Silver
# MAGIC
# MAGIC Three things happen here.
# MAGIC
# MAGIC The gateway writes .NET property names, so columns arrive PascalCase.
# MAGIC Standardizing them to `snake_case` makes the table match the Postgres query
# MAGIC store and keeps SQL readable.
# MAGIC
# MAGIC Deduplication on `event_id` is belt and braces. The gateway already dedupes
# MAGIC on the business key before publishing, but the landing writer flushes in
# MAGIC batches and Service Bus is at-least-once, so a replayed file could carry the
# MAGIC same event twice. The id is derived from source system plus source record
# MAGIC id, so it is stable across replays and safe to dedupe on.
# MAGIC
# MAGIC Filtering on `schema_version` means a future contract version cannot quietly
# MAGIC corrupt this table; it gets its own pipeline instead.

# COMMAND ----------

import re

from pyspark.sql import functions as F
from pyspark.sql.window import Window

SUPPORTED_SCHEMA_VERSION = 1


def to_snake_case(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


renamed = bronze.toDF(*[to_snake_case(column) for column in bronze.columns])

newest_first = Window.partitionBy("event_id").orderBy(F.col("ingested_at_utc").desc())

silver = (
    renamed.where(F.col("schema_version") == SUPPORTED_SCHEMA_VERSION)
    .withColumn("_row", F.row_number().over(newest_first))
    .where(F.col("_row") == 1)
    .drop("_row")
    .withColumn("quantity_mt", F.col("quantity_mt").cast("decimal(12,3)"))
    .withColumn("price_usd_per_mt", F.col("price_usd_per_mt").cast("decimal(12,4)"))
    .withColumn("total_usd", F.col("total_usd").cast("decimal(18,2)"))
    .withColumn("trade_date", F.to_date("traded_at_utc"))
    # How long the gateway took to accept the trade after it happened. A useful
    # freshness signal once real sources replace the simulated ones.
    .withColumn(
        "ingest_lag_seconds",
        F.unix_timestamp("ingested_at_utc") - F.unix_timestamp("occurred_at_utc"),
    )
)

print(f"{renamed.count()} bronze rows -> {silver.count()} silver rows")
display(silver.limit(5))

# COMMAND ----------

silver.write.mode("overwrite").saveAsTable(f"{CATALOG}.{SCHEMA}.silver_bunker_trades")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Both ingestion channels, one table
# MAGIC
# MAGIC The point of the common event contract: a trade pulled from a REST endpoint
# MAGIC on a schedule and one consumed from a Kafka topic land in the same shape and
# MAGIC are queried the same way. Only the `channel` column says which was which.

# COMMAND ----------

# MAGIC %sql
# MAGIC SELECT channel,
# MAGIC        source_system,
# MAGIC        count(*)                    AS trades,
# MAGIC        round(sum(quantity_mt), 1)  AS total_mt
# MAGIC FROM workspace.bunkerflow.silver_bunker_trades
# MAGIC GROUP BY channel, source_system
# MAGIC ORDER BY trades DESC

# COMMAND ----------

# MAGIC %md
# MAGIC ## Gold

# COMMAND ----------

gold = (
    silver.groupBy("port", "product")
    .agg(
        F.count("*").alias("trades"),
        F.round(F.sum("quantity_mt"), 1).alias("total_mt"),
        F.round(F.sum("total_usd"), 2).alias("total_usd"),
        F.round(F.avg("price_usd_per_mt"), 2).alias("avg_price_usd_per_mt"),
        F.countDistinct("vessel_imo").alias("vessels"),
    )
    .orderBy(F.col("total_usd").desc())
)

gold.write.mode("overwrite").saveAsTable(f"{CATALOG}.{SCHEMA}.gold_port_product_volume")

display(gold)

# COMMAND ----------

# MAGIC %md
# MAGIC ## What this does and does not show
# MAGIC
# MAGIC It shows that BunkerFlow's output is a real lakehouse input: the Parquet it
# MAGIC writes reads straight into Delta, the partitioning is picked up as intended,
# MAGIC and both ingestion channels land in one queryable table.
# MAGIC
# MAGIC It does not show a production deployment. The trade data comes from
# MAGIC simulated source systems, and this reads a committed sample rather than a
# MAGIC live feed. Wiring the gateway's landing writer directly at cloud object
# MAGIC storage that Databricks reads is the next step, and it is a configuration
# MAGIC change to `Landing__ParquetRootPath`, not a rewrite.
