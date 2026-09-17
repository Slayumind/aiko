# Update manifest test vector

- `SHA256SUMS.txt` is a manifest in the release format: `<64 lowercase hex>  <file name>`, LF, ASCII.
- `SHA256SUMS.txt.sig` is the DER ECDSA P-256 signature over the raw bytes of the manifest, made with `openssl dgst -sha256 -sign`.
- `public.pem` is the matching public key (SubjectPublicKeyInfo). The private key was deleted right after signing.
- `payload.bin` is the file the manifest lists. The C# tests and the Swift port read these same files.
