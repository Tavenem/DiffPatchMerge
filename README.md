![build](https://img.shields.io/github/workflow/status/Tavenem/DiffPatchMerge/publish/main)[![NuGet downloads](https://img.shields.io/nuget/dt/Tavenem.DiffPatchMerge)](https://www.nuget.org/packages/Tavenem.DiffPatchMerge/)

Tavenem.DiffPatchMerge
==

Tavenem.DiffPatchMerge is a simple diff-patch-merge implementation based on [Google's algorithm](https://github.com/google/diff-match-patch). This library differs from Google's reference .NET implementation mainly in that it simplifies and speeds up operations by only accepting strict, rather than fuzzy matching. It is targeted at use cases where this behavior is either expected or required.

## Installation

Tavenem.DiffPatchMerge is available as a [NuGet package](https://www.nuget.org/packages/Tavenem.DiffPatchMerge/).

## Roadmap

Tavenem.DiffPatchMerge is a relatively stable library which sees minimal development. Although additions and bugfixes are always possible, no specific updates are planned at this time.

## Contributing

Contributions are always welcome. Please carefully read the [contributing](docs/CONTRIBUTING.md) document to learn more before submitting issues or pull requests.

## Code of conduct

Please read the [code of conduct](docs/CODE_OF_CONDUCT.md) before engaging with our community, including but not limited to submitting or replying to an issue or pull request.