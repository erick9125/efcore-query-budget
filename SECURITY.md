# Security

## Reporting

If you discover a security issue, open a private report or contact the maintainer directly. Do not open a public issue for sensitive disclosures.

## Parameter handling

Captured query parameters may contain emails, tokens, identifiers, personal data, or passwords.

By default:

- Reports show `Distinct parameter sets: N`
- Parameter values are hidden
- Binary payloads are hashed for fingerprinting and never printed in full

Use `QueryParameterDisplayMode.Full` only in trusted local diagnostics.
