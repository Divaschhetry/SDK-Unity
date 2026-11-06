# Sentience WaaS Unity SDK

This repository contains the Unity project and package source for the Sentience WaaS API. The manifest at `Assets/package.json` names the package `xyz.0xSentience.waas-unity` version 2.4.2 and targets Unity 2021.3.

## Open the project

1. Install Unity 2021.3.6f1, the version recorded in `ProjectSettings/ProjectVersion.txt`.
2. In Unity Hub, add this repository folder and open it with that editor version.
3. Allow Unity to resolve the dependencies listed in `Packages/manifest.json` on first open.

## Import package samples

The package manifest lists three samples under `Assets/Samples~`: Setup, Demo Scene, and Useful Scripts. In a project consuming the package through Unity Package Manager, import a sample from the package's Samples section. Unity copies the imported sample into the consuming project's `Assets/` folder. See Unity's [sample package documentation](https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/cus-pkg-lp/cus-pkg-development/cus-samples) for details.

The demo scene sample is not included in this repository's build scene list. After importing it into this project, add the imported scene in File > Build Settings if you want it in a player build.

## Tests

The test-chain uses Node.js and Yarn. The Makefile uses Bash and Make. Install its dependencies with:

```sh
make bootstrap
```

The Unity test targets expect the editor executable to be available as `Unity` on `PATH`. Available targets include:

- `make test`: starts the local Hardhat test chain and runs Unity EditMode and PlayMode tests.
- `make test-sdk`: starts the local Hardhat test chain and runs Unity EditMode tests.
- `make test-ui`: runs Unity PlayMode tests.

## Repository layout

- `Assets/SentienceSDK`: SDK source and tests.
- `Assets/SentienceExamples`: example code and scenes.
- `Assets/Samples~`: package samples intended for import into a consuming project.
- `testchain`: Hardhat test-chain project.
- `Packages` and `ProjectSettings`: Unity project configuration.

## Resources

- [Sentience WaaS Unity documentation](https://docs.sentience.xyz/unity-waas-sdk)
- [Package repository listed in the manifest](https://github.com/0xSentience/Sentience-unity)

See [LICENSE](LICENSE) for the license text.

## Build settings validation

Run `python3 .github/scripts/validate_build_settings.py` to verify that every scene listed in Unity Build Settings exists in the repository and that its GUID matches the scene's `.meta` file. GitHub Actions runs this check for pull requests targeting
`master` and pushes to `master`. It uses Python's standard library and does not require
a Unity license.
