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
6. Every request is auditable to a subject and a correlation id.

## Deployment expectations

CloudFilm is one layer of a hospital's controls. Operators are expected to terminate
TLS at the edge, pin third-party images to digests, restrict the Retrieve Service's
egress to the specific hospital endpoints it integrates with, rotate
`JWT_SIGNING_KEY` on a schedule, and back up the PostgreSQL volume.
