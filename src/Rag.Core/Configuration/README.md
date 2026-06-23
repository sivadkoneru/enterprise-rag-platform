# Rag.Core Configuration

Options models and validation for environment-bound configuration.

All provider settings should bind through typed options. Avoid direct environment reads in business logic.

Most options classes live here and are bound by `AddRagPlatform`, including `DocumentStoreOptions`,
`JobStoreOptions`, and `VectorStoreOptions` (the provider-selector settings, shared by both the
in-Core and the opt-in provider implementations). `S3Options` and `AzureBlobOptions` are the
exception: they live in `Rag.Providers.Aws` and `Rag.Providers.AzureBlob` respectively, keep the
`Rag.Core.Configuration` namespace, and are bound by that project's own `AddRagAwsS3` /
`AddRagAzureBlob` extension rather than by `AddRagPlatform`, since `Rag.Core` cannot reference a type
it does not own the assembly for.

