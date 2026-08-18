# Platform Operations Runbook

Operational procedures for the platform team. This runbook covers systems, not people policy.

## Nightly Batch

The nightly reconciliation batch starts at 02:15 UTC and typically completes in forty minutes. A run
that exceeds ninety minutes should be halted and restarted from the last checkpoint.

## Certificate Rotation

TLS certificates are rotated automatically twenty days before expiry. Failed rotations page the
platform on-duty engineer through the alerting pipeline.

## Database Failover

Primary database failover is automatic. Manual failover requires two platform engineers and is
performed only during a declared maintenance window.

## Backup Verification

Backups are restored into an isolated environment every week and verified by checksum. A backup that
fails verification is regenerated immediately.
