# Mock data

Fixtures that stand in for the CloudFilm API until it exists.

## Why this is a separate directory, not a file in `src/`

`src/` is the contract: types that describe what the API will send. This
directory is *sample content*. Keeping them physically apart means:

- the boundary is visible in an import statement — `@cloudfilm/shared/mock` is
  impossible to reach by accident;
- deleting the fixtures is one directory removal plus one line in
  `package.json`, with the compiler listing every place that depended on them;
- no fixture can ever be pulled in as a transitive dependency of the contract
  types, because the dependency points the other way.

## Rules

- No business logic here. Lookup helpers only — anything that makes a decision
  belongs in the application.
- Nothing in `src/` may import from this directory.
- Study Instance UIDs use the DICOM-reserved `1.2.826.0.1.3680043.8.498.*`
  prefix so a stray fixture can never collide with a real hospital identifier.

## Removing it

When the API serves patients, replace `findPatientRecord` at its call site with
the API client, delete this directory, and drop the `./mock` entry from
`packages/shared/package.json`. The build will point at anything left behind.
