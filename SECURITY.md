# Security policy

## Reporting a vulnerability

Please **do not open a public issue** for a suspected vulnerability.

Use GitHub's private vulnerability reporting instead:
<https://github.com/protobuf-net/protobuf-net.Grpc/security/advisories/new>. That opens a private
thread visible only to the maintainers, and is the fastest route to a fix and an advisory.

If that is unavailable to you for any reason, email <marc.gravell@gmail.com> with `protobuf-net.Grpc
security` in the subject.

Please include enough to reproduce: the package and version, the target framework, and a minimal
service contract or payload that shows the problem.

## What is in scope

This repository is code-first gRPC for .NET: it turns .NET interfaces into gRPC services, and moves
payloads through protobuf-net. The things worth reporting are broadly:

- a payload that causes unbounded allocation, non-terminating work, or a crash while being
  marshalled or unmarshalled;
- a service binding that exposes more than the contract declares - a method reachable that should
  not be, or endpoint metadata (`[Authorize]` and friends) not carried onto the endpoint;
- anything that lets a client influence server state outside the declared contract.

Serialization of *untrusted* input is the interesting surface. Note that protobuf-net itself is a
separate repository - <https://github.com/protobuf-net/protobuf-net> - and a problem in the
serializer proper belongs there; if you are unsure which, report it here and it will be moved.

## Supported versions

Fixes land on `main` and ship in the next release of the affected package. There is no long-term
servicing branch, so "supported" means the current release line.
