# Verified against real Azure

The Terraform in `infra/terraform` was applied to a real Azure subscription on
4 August 2026, the gateway was pointed at the resulting namespace, and the two
design features that could not be exercised locally were tested. The resources
were destroyed afterwards, so this file is the record.

Everything below is CLI output from the live deployment, not a description of
intent.

## What was deployed

```
== namespace ==
Name                             Sku       Location     Tls    Status
-------------------------------  --------  -----------  -----  --------
sbns-bunkerflow-dev-northeurope  Standard  northeurope  1.2    Active

== topic ==
Name               DuplicateDetection    Window    Ordering
-----------------  --------------------  --------  ----------
bunkerflow-events  True                  PT10M     True

== subscription ==
Name     MaxDelivery    DlqOnExpiry    DlqOnFilterError
-------  -------------  -------------  ------------------
landing  5              True           True

== subscription rule ==
Name       FilterType    Sql
---------  ------------  -----------------
schema-v1  SqlFilter     schemaVersion = 1

== dead-letter queue ==
Name                   MaxDelivery    DlqOnExpiry
---------------------  -------------  -------------
bunkerflow-deadletter  5              True

== authorization rules ==
topic/gateway-send rights:            Send
topic/landing-listen rights:          Listen
queue/gateway-deadletter-send rights: Send
```

## Least privilege actually holds

The gateway published 47 records with a **Send-only** credential and the landing
worker consumed all 47 with a **Listen-only** one. Neither can do the other's
job, and neither has Manage, so neither can create or delete entities.

```
landedEvents: 47
bunkerflow_records_total{outcome="accepted",channel="batch"} 47
```

This is the change that applying the Terraform forced. Against the emulator a
single connection string did everything, so the least-privilege rules were
decorative: the landing worker would have failed to receive, and the
dead-letter sink would have failed to address the queue, because per-entity
rules produce entity-scoped connection strings. The application now takes three
credentials rather than one.

## Duplicate detection, proved

The publisher derives its `MessageId` from the business key, so a replayed
source record produces the same id and the broker can reject it before any
consumer is involved.

The application dedupe store normally stops a replay long before that, so the
test clears the reservation between two identical ingests. The gateway then
genuinely publishes the same trade twice and the broker is the only thing left
that can prevent it landing twice.

```
==> first ingest
    HTTP 202
    rows in landing store: 1

==> clearing the application dedupe reservation
    DELETE 1

==> second ingest of the identical record
    HTTP 202
    rows in landing store: 1

PASS: the broker discarded the replay. One trade, one row.
```

## The subscription filter, proved

The subscription carries `schemaVersion = 1`, so a future contract version gets
its own consumer instead of quietly corrupting this one.

The gateway can only ever stamp version 1, so proving the filter needed a
producer that could lie about it. Two well-formed events were sent straight to
the topic, identical apart from the `schemaVersion` application property.

```
sent schemaVersion=1 as FILTER-v1-1785826676
sent schemaVersion=2 as FILTER-v2-1785826676

what reached the landing store:
    FILTER-v1-1785826676|1

    schemaVersion=1 landed: 1
    schemaVersion=2 landed: 0

PASS: the filter passed v1 and withheld v2.
```

## Notes

`westeurope` refused the namespace with `RequestDisallowedByAzure: The selected
region is currently not accepting new customers`. That is a capacity
restriction on new subscriptions, not a configuration error; `northeurope`
accepted it unchanged, which is nearer the Nordics anyway.

The resources were destroyed after this run. Re-creating them is
`scripts/tf.sh apply -var location=northeurope` against a subscription you are
logged into.
