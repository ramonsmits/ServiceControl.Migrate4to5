# End-to-end acceptance checklist (release gate)

Manual procedure run once per release candidate before `sc-migrate` ships. Requires a Windows VM
(the exporter is `net48`/Windows-only) and two ServiceControl error-instance installs: **4.33.5**
(source) and **5.11.x** (target). See the [design spec](superpowers/specs/2026-07-03-sc4to5-data-migration-design.md)
for background and [`../COLLECTIONS.md`](../COLLECTIONS.md) for what each collection means.

Each step lists the exact command and what to confirm before moving on. Do not proceed past a
step whose expected observation doesn't hold — treat that as a release blocker.

## 1. Seed a real SC 4.33.5 instance

- [ ] Install ServiceControl **4.33.5** as an error/primary instance on the Windows VM, transport
      **Learning** or **MSMQ** (either is fine — the transport is irrelevant to the migration path;
      pick whichever is faster to stand up).
- [ ] Seed **at least 20 failed messages** spread across **at least 3 distinct endpoints**, so the
      export/import exercises more than one `SendingEndpoint`/`ReceivingEndpoint` value and more
      than one failure group.
- [ ] Among those, make sure at least one message has a body **larger than 85,000 bytes** (the
      `MsgFullText` cutoff — confirms large bodies migrate as attachments, not inline/full-text) and
      at least one has a **binary body** (e.g. a non-text/non-XML/non-JSON content type — confirms
      binary bodies migrate as an attachment with the original content type, no full-text
      re-creation).
- [ ] **Archive** a handful of the seeded messages (via ServicePulse or `POST api/errors/{failedMessageId}/archive`)
      so both `Unresolved` and `Archived` status values are represented.
- [ ] Add a **comment to a failure group** in ServicePulse (Recoverability → group → comment), or:

  ```bash
  curl -X POST http://localhost:33333/api/recoverability/groups/<groupId>/comment \
    -H "Content-Type: application/json" -d "\"Investigated, waiting on partner team\""
  ```

- [ ] Create a **message redirect** (retry redirect from one queue to another) in ServicePulse
      (Recoverability → Redirects), or via the redirects API.

**Expected observation:** `GET http://localhost:33333/api/errors` returns >=20 messages spanning
>=3 endpoints and both `Unresolved` and `Archived` statuses; `GET http://localhost:33333/api/redirects`
returns the redirect just created; the failure group shows the comment in ServicePulse.

## 2. Stop the instance and export

- [ ] Stop the SC4 error instance (Windows service or console process).
- [ ] Run the exporter against the instance's `DbPath` (from its config, typically
      `C:\ProgramData\Particular\ServiceControl\<InstanceName>`):

  ```bash
  sc-migrate-export export --db-path "C:\ProgramData\Particular\ServiceControl\Particular.ServiceControl" --out D:\dumps\e2e-dump --error-retention 15.00:00:00
  ```

**Expected observation:** command exits `0`; console output lists a per-collection line for
`FailedMessages`, `GroupComments`, `MessageRedirects`, etc. with `exported:` counts matching what
was seeded (>=20 for `FailedMessages`, >=1 for `GroupComments` and `MessageRedirects`); `D:\dumps\e2e-dump\manifest.json`
exists and its `BodyCount`/`BodyTotalBytes` are non-zero.

## 3. Install SC 5.11.x and import

- [ ] Install ServiceControl **5.11.x** as a fresh error/primary instance (or point at the same
      machine post force-upgrade).
- [ ] Start it in **maintenance mode** so the importer has a server to talk to without live
      ingestion running yet:

  ```bash
  ServiceControl.exe --maintenance
  ```

- [ ] Run the importer:

  ```bash
  sc-migrate import --in D:\dumps\e2e-dump --url http://localhost:33334 --database primary --error-retention 15.00:00:00
  ```

  **Expected observation:** exits `0`; per-collection `imported:` counts match the export's
  `exported:` counts (`skipped (existing): 0` on a first run against a fresh database); `warnings: 0`
  (a body-missing warning here would mean a seeded message's body wasn't captured — investigate
  before continuing).

- [ ] Run the verifier:

  ```bash
  sc-migrate verify --in D:\dumps\e2e-dump --url http://localhost:33334 --database primary --error-retention 15.00:00:00
  ```

  **Expected observation:** prints `VERIFY PASSED` and **exits `0`**. This is the release gate —
  a non-zero exit or any `PROBLEM:` line is a blocker; do not continue to step 4 until it's clean.

## 4. Start SC5 normally and validate against ServicePulse

- [ ] Stop the maintenance-mode process; start the SC5 instance normally, and start (or point)
      ServicePulse at it.
- [ ] **Failed-message counts per status match.** Compare SC4's pre-migration counts to SC5's:

  ```bash
  curl http://localhost:33333/api/errors | jq '[.[].status] | group_by(.) | map({status: .[0], count: length})'
  curl http://localhost:44444/api/errors | jq '[.[].status] | group_by(.) | map({status: .[0], count: length})'
  ```

  **Expected observation:** `Unresolved` and `Archived` counts on SC5 match SC4's (allowing for any
  messages skipped for being past `--error-retention` on both sides).

- [ ] **Bodies render** for the small, large (>85KB), and binary messages — open each in
      ServicePulse's message body viewer, or:

  ```bash
  curl http://localhost:44444/api/messages/<messageId>/body
  ```

  **Expected observation:** small text body full-text renders inline; large body downloads/displays
  as an attachment rather than inline full-text; binary body downloads with its original content
  type and correct byte size (compare against the SC4 body via the same endpoint on port `33333`
  before it was stopped, or against a byte count noted in step 1).

- [ ] **Group comment present.**

  ```bash
  curl http://localhost:44444/api/recoverability/groups/<classifier> | jq '.[] | select(.id=="<groupId>") | .comment'
  ```

  **Expected observation:** the comment text from step 1 is present, unchanged.

- [ ] **Redirect present.**

  ```bash
  curl http://localhost:44444/api/redirects
  ```

  **Expected observation:** the redirect created in step 1 appears with the same from/to queue names.

- [ ] **Retry of a migrated message succeeds end-to-end.** Pick one migrated `Unresolved` message
      and retry it, either in ServicePulse or:

  ```bash
  curl -X POST http://localhost:44444/api/errors/<failedMessageId>/retry
  ```

  **Expected observation:** the endpoint receiving the retried message processes it successfully
  (or fails again with the same original error, if the underlying cause wasn't fixed — either way,
  the message is picked up by the transport and dispatched, proving retry redirect/queue wiring
  survived the migration) and the message's status updates in ServicePulse.

If every expected observation above holds, the release candidate passes the end-to-end acceptance
gate.
