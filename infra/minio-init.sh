#!/bin/sh
# Idempotent MinIO bootstrap for the Nimbus production stack (issue #95).
#
# Runs once per `docker compose up` via the `minio-init` service (minio/mc image),
# using the ROOT credentials only for this bootstrap. It:
#   1. creates the four buckets the application needs (per issue #11's convention)
#   2. enables versioning on each (confirmed supported on this single-node topology
#      — see infra/MINIO.md "Versioning outcome")
#   3. explicitly denies anonymous/public access on each bucket
#   4. creates a least-privilege policy scoped to only those four buckets
#   5. creates (or reuses) a dedicated application access key and attaches the
#      policy to it — this key, not the root user, is what the API authenticates
#      with (MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY in .env)
#
# Every step is safe to re-run: `mc mb --ignore-existing`, `mc admin policy create`
# (overwrites in place), `mc admin user add` (updates the secret if the user
# exists), and `mc admin policy attach` (no-op if already attached) all tolerate
# a rebuild with no console clicking required.
set -eu

BUCKETS="flight-images flight-tracks flight-exports map-cache"
POLICY_NAME="nimbus-app"
POLICY_FILE="/tmp/nimbus-app-policy.json"

echo "Waiting for MinIO API at ${MINIO_ENDPOINT}..."
until mc alias set local "${MINIO_ENDPOINT}" "${MINIO_ROOT_USER}" "${MINIO_ROOT_PASSWORD}" >/dev/null 2>&1; do
	sleep 2
done
echo "MinIO is reachable."

for bucket in ${BUCKETS}; do
	mc mb --ignore-existing "local/${bucket}"
	mc version enable "local/${bucket}"
	# Belt-and-braces: buckets are private by default, but make it explicit and
	# idempotent rather than assumed (see #95 "anonymous access verified impossible").
	mc anonymous set none "local/${bucket}"
done

cat > "${POLICY_FILE}" <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:ListBucket"],
      "Resource": [
        "arn:aws:s3:::flight-images", "arn:aws:s3:::flight-images/*",
        "arn:aws:s3:::flight-tracks", "arn:aws:s3:::flight-tracks/*",
        "arn:aws:s3:::flight-exports", "arn:aws:s3:::flight-exports/*",
        "arn:aws:s3:::map-cache", "arn:aws:s3:::map-cache/*"
      ]
    }
  ]
}
EOF

mc admin policy create local "${POLICY_NAME}" "${POLICY_FILE}"

# The application key is a normal MinIO user (not root, no admin actions in the
# policy above), scoped by MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY in .env —
# generated once at deploy time and never the root credential pair.
mc admin user add local "${MINIO_APP_ACCESS_KEY}" "${MINIO_APP_SECRET_KEY}"
mc admin policy attach local "${POLICY_NAME}" --user "${MINIO_APP_ACCESS_KEY}"

rm -f "${POLICY_FILE}"

echo "MinIO bootstrap complete: buckets=[${BUCKETS}] policy=${POLICY_NAME} app-user=${MINIO_APP_ACCESS_KEY}"
