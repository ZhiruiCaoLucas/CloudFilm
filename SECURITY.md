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
6. Sign-in outcomes, successful reads of patient data, and every act that widens
   who may see a study are recorded in a durable audit trail: `LOGIN_SUCCEEDED`,
   `LOGIN_FAILED`, `VIEW_PATIENT`, `VIEW_STUDY`, `SEARCH_STUDIES`,
   `MARK_STUDY_READ`, `MARK_STUDY_UNREAD`, `GRANT_CREATED`, `GRANT_REVOKED`,
   `SHARE_LINK_CREATED`, `SHARE_LINK_REVOKED`, `SHARE_LINK_VIEWED`,
   `DOWNLOAD_STUDY` and `ANALYSE_STUDY`, each with the acting account where there
   is one, the resource
   identifier and the request's correlation id. Anonymous acts — redeeming or
   downloading a share link — record no actor rather than an invented one, and no
   entry ever carries a token, a digest or a session identifier. This is not a log of every HTTP request - only of the events
   above. A failed sign-in records no e-mail address, and one action covers every
   rejection reason, so the trail cannot be used to discover which accounts exist.
7. An access to patient data that could not be audited does not happen: if the
   audit write fails, the request fails and the data is not returned.

## Share links

An administrator can issue a share link for one study. The link is a bearer
credential: whoever holds the URL can use it, and nothing else is required — no
account, no password, no role.

What the design guarantees:

- **One study.** A link names a single Study Instance UID, and the download
  endpoint takes the study from the link rather than from the request. There is
  no parameter a holder could edit to reach another study, another patient or
  that patient's other studies.
- **Viewing, or viewing and downloading.** A link carries one of two
  permissions, and it is enforced by the route the holder calls:
  - `View` — `POST /api/v1/share/{token}/viewer-session` issues a permit to open
    the study in the viewer. `GET /api/v1/share/{token}/download` refuses a view
    link with the same empty `404` an unknown token gets.
  - `Download` — both routes answer. Viewing is covered because Download
    satisfies View in the permission ladder; the reverse is not true.

  Neither permission reaches any other CloudFilm endpoint.
- **Stored as a digest.** Only `SHA-256(token)` is persisted. The plaintext is
  returned once, by the request that created the link, and cannot be recovered
  afterwards — a lost link is re-issued, not looked up.
- **Always expires.** An expiry is mandatory and capped at seven days.
- **Revocable.** An administrator can withdraw a link, and the withdrawal takes
  effect on the next request rather than at the next expiry.
- **Audited.** A successful download records `DOWNLOAD_STUDY` against the study,
  with no actor, before any bytes are sent. If that record cannot be written, the
  request fails and the study is not returned.
- **Indistinguishable refusals.** A malformed token, an unknown one, an expired
  one, a withdrawn one, one whose permission does not cover what was asked, and a
  study the archive no longer holds all answer `404` with an empty body.

What it does not do:

- **A token in a URL is a secret in a place that keeps records.** The token is in
  the share URL, so it will appear in the recipient's browser history, in
  `Referer` headers, in any reverse proxy's access log, and in CloudFilm's own
  request log — the API logs the request path, and the path contains the token.
  What the token never reaches is the database, the audit trail, a cache key, or
  an exception message. Treat a shared link as compromised once it has been
  forwarded, and revoke it when the recipient is finished.
- **Redemption is rate limited, not rationed.** A link may be exchanged for a
  viewer session at most twenty times a minute, counted against the token's
  digest, and downloaded at most twenty times a minute on its own separate count.
  Exceeding either answers the same empty `404`. This bounds what a leaked link
  can be turned into; it is not what makes a token hard to guess.
- **A viewing link admits its holder to the viewer for fifteen minutes at a
  time.** Redeeming one records `SHARE_LINK_VIEWED` once, against the study, with
  no actor; the DICOMweb requests that follow are checked against the session and
  are not individually audited. See "Viewer access: the DICOMweb gate".
- **`View` controls access, not what happens after it.** A view link decides who
  may open the study and which study they may open. It cannot stop a recipient
  keeping the pixels the viewer has legitimately rendered for them: the images
  are in their browser, and a screenshot, a developer console or a saved network
  response is enough. This is inherent to client-side viewing and no
  configuration changes it. The distinction `View` makes is that CloudFilm does
  not hand over the study *file* — the DICOM, with every tag and every instance —
  which is what `Download` is for.

## Viewer access: the DICOMweb gate

The viewer reaches pixel data over a second path that never touches the API:

```
Browser → :8081 (OHIF) → nginx → DICOMweb → Orthanc
```

nginx injects Orthanc's Basic credential server-side, so nothing on that path can
be limited by the API's bearer tokens. It used to be limited by nothing at all:
anyone who could reach `:8081` and knew a Study Instance UID could open that
study without a CloudFilm account. A Study Instance UID is not a secret — it is a
structured identifier, it appears in DICOM sent to other systems, and it is
visible in the workspace URL.

That is now closed by a **viewer session**.

- Every request under `/dicom-web/` is authorised first. nginx issues an
  `auth_request` subrequest to `GET /api/v1/share/dicom-web/authorize`, passing
  the original URI and the browser's cookies, and proxies to Orthanc only on a
  `204`.
- A viewer session is an opaque identifier in an `HttpOnly` cookie, bound to **one
  Study Instance UID**, one permission and an expiry, and held in Redis with a
  fifteen minute lifetime. It carries no patient data.
- Both kinds of viewer use the same mechanism. A signed-in clinician gets a
  session from `POST /api/v1/imaging/studies/{uid}/viewer-session`, checked
  against the same study access policy the study endpoint uses; a share-link
  recipient gets one from `POST /api/v1/share/{token}/viewer-session`. They are
  indistinguishable afterwards, so the gate has a single rule to enforce.
- **A session admits one study.** A request whose path names another study is
  refused; a DICOMweb search is admitted only when its query names the session's
  study. An unfiltered `/dicom-web/studies` — the archive's entire index — is
  refused.
- **Revocation is immediate, for share links.** A session issued from a share
  link records which link, and the gate checks on **every** DICOMweb request that
  the link is still live — not withdrawn, not expired. A link revoked or expiring
  mid-study stops the next image rather than the next session.
- **A session issued to a signed-in clinician is not re-checked.** It records no
  link, so the gate has nothing to re-ask, and the caller's entitlement is
  evaluated once, when the session is issued. Removing a role or disabling an
  account therefore stops the *next* session rather than the current one, which
  can be up to fifteen minutes away. Signing out does not end a viewer session
  either; it ends the portal session that could mint another.
- **Refusals are uniform.** The gate answers `401`, which nginx rewrites to an
  empty `404`. A missing cookie, an expired session, a withdrawn link and a
  request for another patient's study are externally identical.
- **Orthanc is not published.** Neither `docker-compose.yml` nor
  `docker-compose.dev.yml` maps port 8042 to the host, so Orthanc is reachable
  only from the internal Docker network — by the Retrieve Service and by the
  viewer's proxy, which now authorises every request. This is a property of the
  supplied configuration, not a property of Orthanc: a developer who deliberately
  publishes 8042 with a local override, as `docs/local-imaging.md` describes for
  seeding, has an unauthenticated archive on their host for as long as the
  override is in place. Nothing in CloudFilm prevents that, and nothing should
  rely on it being impossible.

What this still does not do:

- **It does not authenticate the viewer's static assets.** `:8081` continues to
  serve the OHIF application itself to anyone who can reach it. What it will not
  serve them is imaging.
- **The portal and the viewer must share a hostname.** The cookie is host-only:
  it carries no `Domain` attribute, and cookies ignore ports, so a portal on
  `:3000` and a viewer on `:8081` of the same host share one. Serving them from
  different hostnames — `portal.example.com` and `viewer.example.com` — would
  need a cookie domain covering both, which is **not yet configurable**. Until it
  is, put the viewer on a path or a port of the portal's own host.
- **A viewer session is a bearer credential for its fifteen minutes.** It is
  `HttpOnly` and study-scoped, which bounds what a stolen one is worth, but it is
  not bound to the browser that obtained it.
- **`:8081` should still not be published beyond the network where CloudFilm
  itself is reachable.** The gate is a control, not a reason to widen exposure.

## The Retrieve Service

The Retrieve Service authenticates nobody. It answers whoever reaches it — with
patients, studies, searches and whole DICOM studies — and it is the component
that holds Orthanc's credential. Its only protection is that it is not reachable.

- **In production it is not published at all.** `docker-compose.yml` puts it on
  the internal network with no host port. The API reaches it by container name;
  nothing outside the compose network can.
- **In development it is published on loopback only**, as
  `127.0.0.1:8082:8080` in `docker-compose.dev.yml`. That keeps
  `http://localhost:8082` working for a developer running the API from source,
  and stops the same machine offering the archive to its network. It is a
  development convenience and never an access path: a request that arrives there
  has passed no authentication, no role check, no access grant, no share link and
  no DICOMweb gate.
- **Publishing it on `0.0.0.0` — or through a reverse proxy — would defeat every
  control in this document.** If a deployment needs Retrieve reachable from
  another host, that is a new design conversation, not a port mapping.

## The analysis service

An internal Python service that receives one study's DICOM and reports structural
facts about it. It is not a diagnostic model and produces no clinical finding.

Like the Retrieve Service, it **authenticates nobody**, and for the same reason its
only protection is that it cannot be reached:

- **It publishes no port**, in either compose file. It is on the `internal` network
  and is addressed only by the API, at `http://analysis:8000`.
- **Authorisation happens before it is involved.** The API requires an application
  role, then asks `IStudyAccessPolicy` for the `Download` permission on that study,
  and only then fetches the bytes. A denied caller is answered `404` and no study is
  retrieved.
- **It cannot choose a study.** The port hands it an open stream, never an
  identifier or a URL, so it has nothing to resolve and makes no outbound request of
  any kind - it depends on no HTTP client at all.
- **One `ANALYSE_STUDY` entry** is written per successful analysis, against the
  study the caller was authorised for, inside the same transaction. If the entry
  cannot be written the request fails and no result is returned.
- **It is inside the PHI boundary.** DICOM carries patient identifiers in its own
  tags, so the service holds PHI whatever the request envelope says. It is operated
  accordingly: nothing about a dataset is logged, parser messages are never passed
  on, the response carries only the agreed fields, and nothing is written to disk by
  the application or kept between requests.
- **It is optional.** Without `Downstream:AnalysisServiceUrl` the API registers no
  analyser and the endpoint answers `501`. A deployment without it is complete.

What it does not do:

- **It does not de-identify anything.** The study reaches it with its tags intact.
  Treat the container as holding PHI for the duration of a request, and note the
  temporary-file caveat in `apps/analysis/README.md`: the web framework spools large
  uploads to the container's temporary directory, which is a deployment concern
  (`tmpfs`) rather than something the application can turn off.
- **It performs no clinical function.** Nothing it returns is a diagnosis, and the
  portal states so wherever a result appears.

## Staged limitations

Known and deliberate, recorded so they are not mistaken for defects:

- **Access grants are not currently reachable.** The grant tables, management API
  and `IStudyAccessPolicy` are implemented and tested, but every study endpoint
  requires an application role and every application role is admitted to the whole
  catalogue, so the `GrantedStudyOnly` scope cannot occur in the deployed
  configuration. Grants therefore neither widen nor narrow anyone's access today.
  Connecting them needs a recipient access path, which is a product decision and
  not a defect in the mechanism.
- **Share links cannot be listed.** An administrator can issue a link and revoke
  one by its identifier, but there is no endpoint that enumerates the links in
  force on a study. An identifier lost is a link that stands until it expires —
  which is at most seven days, and why the cap exists.

## Deployment expectations

CloudFilm is one layer of a hospital's controls. Operators are expected to terminate
TLS at the edge, pin third-party images to digests, restrict the Retrieve Service's
egress to the specific hospital endpoints it integrates with, rotate
`JWT_SIGNING_KEY` on a schedule, and back up the PostgreSQL volume.
