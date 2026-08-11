# Explorations

An exploration records research and user-owned decisions before one or more semantic changes are
committed. It is non-normative: accepted behaviour remains in `azimuth/model/`, and each proposed
transition remains in `azimuth/changes/`.

Create one with:

```text
azimuth explore create <id> --title <title>
```

`exploration.md` is the only anchor. Add `research.md` when sourced facts would obscure it and
`change-map.md` when the result genuinely spans several changes. A downstream proposal declares
the exploration and decision ids it carries. Archive the exploration after every decision has a
disposition and the resulting changes, experiments or abandonment are identified; do not wait for
all downstream delivery to complete.
