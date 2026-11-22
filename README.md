# Mosu client — public source snapshot

This is a public source snapshot of the Morasooma client. It is based on
[ppy/osu](https://github.com/ppy/osu) and includes work derived from the former
GooGuTeam client fork.

## Build the desktop client

Install the .NET 10 SDK, Python 3.12 or newer, and FFmpeg with `libvorbis` on
`PATH`. Then run:

```sh
git clone --recurse-submodules https://github.com/morasooma/mosu.git
cd mosu
dotnet build osu.Desktop/osu.Desktop.csproj -c Debug -m:4
```

For an existing checkout, run `git submodule update --init` before building.
The [Morasooma resource bank](https://github.com/morasooma/mosu-resources-public)
is pinned as a separate Git submodule. It does not contain translation
catalogues. The forked framework and its patched Veldrid renderer are included
under `external/mosu-framework`. The client also uses public ppy resource
packages. Each dependency retains its own licence.

## Licence and trademarks

The code inherited from osu! is available under the MIT licence in
[LICENCE](LICENCE). The resource submodule has its own licences and notices.

This project is not an official ppy product. The MIT licence for the code does
not grant rights to use the osu! or ppy trademarks outside the terms allowed by
their owners.
