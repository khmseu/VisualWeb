# Official standards cache

[manifest.json](manifest.json) is the tracked registry of official source URLs,
document IDs and fetch/check metadata. Generated HTML lives in the local,
git-ignored `cache/` directory. Keep existing copies for offline work.

From the repository root, bootstrap missing files or refresh documents last
checked more than 30 days ago:

```sh
dotnet run --project tools/SpecCache -- specs/manifest.json
```

See the [standards index](../docs/standards.md) for ownership and interface
citation conventions and the [tool README](../tools/SpecCache/README.md) for
failure/refresh behavior. This cache is independent of tests and conformance
fixtures. Retain upstream notices and follow each publisher's licensing terms.
