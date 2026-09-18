# Code signing policy

Firaw - TaskBar publishes Windows release artifacts from this public repository.
The project plans to use the following service after its application is
approved: **Free code signing provided by SignPath.io, certificate by SignPath
Foundation**.

A signature under this policy confirms the origin of a release artifact. It
doesn't replace source review or guarantee that software is free from defects.

## Source and builds

- Source repository: <https://github.com/firawynix/folderpin>
- License: [MIT](LICENSE)
- Release artifacts must come from a tagged revision in this repository.
- Build scripts and dependency versions are part of the reviewed source.
- Signing requests require manual approval after the artifact and version are
  checked against the release revision.

## Team roles

The project currently has one maintainer. Until more maintainers join, Hugo
Leonardo Lima de Mendonça (`firawynix`) holds these roles:

- Committer and reviewer:
  [GitHub profile](https://github.com/firawynix)
- Signing approver:
  [GitHub profile](https://github.com/firawynix)

Contributions from people without direct commit access require review before
merge. Changes to build, packaging, or signing files receive the same review as
application code.

## Privacy and system changes

Read the [privacy policy](privacy.md). The installer and application describe
their user-visible system changes, and the project provides an uninstaller.

## Reporting concerns

Report suspected compromised or incorrectly signed releases through the
repository's private security reporting feature. For other issues, use the
[public issue tracker](https://github.com/firawynix/folderpin/issues).
