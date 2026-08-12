# Security policy

CloudFilm handles protected health information. Treat every defect with a
confidentiality, integrity or availability impact as a security issue.

## Reporting a vulnerability

Report privately through GitHub's **Report a vulnerability** button on the Security
tab. Do not open a public issue.

Please include: affected component and version, reproduction steps, observed impact,
and whether you believe patient data is reachable.

We aim to acknowledge within three working days and to ship a fix or mitigation for
confirmed high-severity issues within fourteen days.

## Scope

In scope: the CloudFilm portal, API, Retrieve Service, the Compose topology, and the
configuration supplied for Orthanc and OHIF.

Out of scope: vulnerabilities in Orthanc, OHIF, PostgreSQL or Redis themselves —
report those upstream. If CloudFilm's *configuration* of one of them is unsafe, that
is in scope.

## Design commitments

These are properties CloudFilm intends to hold. A report showing any of them is
violated is a valid finding:

1. The hospital PACS is reachable only from the Retrieve Service.
2. Orthanc, PostgreSQL and Redis are never reachable from outside the compose network.
3. Orthanc credentials never reach a browser.
4. Imaging data outside the hospital is transient and expires without operator action.
5. Error responses never disclose internal hostnames, connection strings or stack traces.
6. Sign-in outcomes and successful reads of patient data are recorded in a durable
   audit trail: `LOGIN_SUCCEEDED`, `LOGIN_FAILED`, `VIEW_PATIENT` and `VIEW_STUDY`,
   each with the acting account, the resource identifier and the request's
   correlation id. This is not a log of every HTTP request - only of the events
   above. A failed sign-in records no e-mail address, and one action covers every
   rejection reason, so the trail cannot be used to discover which accounts exist.
7. An access to patient data that could not be audited does not happen: if the
   audit write fails, the request fails and the data is not returned.

## Known limitation: API authorisation does not cover the viewer

Every CloudFilm API endpoint that returns patient data requires a bearer token; the
authorisation fallback policy makes an unauthenticated request to `/api/v1/patients`,
`/api/v1/patients/{mrn}` or `/api/v1/studies/{uid}` answer `401`.

**That does not protect the images.** The viewer reaches pixel data over a second,
independent path that never touches the API:

```
Browser → :8081 (OHIF) → nginx → DICOMweb → Orthanc
```

nginx injects Orthanc's Basic credential server-side, so anyone who can reach port
8081 and knows a Study Instance UID can open that study without a CloudFilm account.
Locking the API down does not change this, because the API is not on that path.

A Study Instance UID is not a secret: it is a structured identifier, it appears in
DICOM sent to other systems, and it is visible in the workspace URL.

Until this is closed, treat `:8081` as an internal endpoint and do not publish it
beyond the network where CloudFilm itself is reachable. Closing it properly means
putting an authenticating proxy in front of DICOMweb that validates the same tokens
the API does, which is a change to the imaging path and is not yet scheduled.

## Deployment expectations

CloudFilm is one layer of a hospital's controls. Operators are expected to terminate
TLS at the edge, pin third-party images to digests, restrict the Retrieve Service's
egress to the specific hospital endpoints it integrates with, rotate
`JWT_SIGNING_KEY` on a schedule, and back up the PostgreSQL volume.
