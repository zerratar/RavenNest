# Art masters

Full resolution sources for the dashboard art band. **Nothing in here is
deployed**: it sits outside `wwwroot` on purpose, so the 18 MB of masters stay
in the repository without being published with the site.

What ships is `src/RavenNest.Blazor/wwwroot/imgs/dashboard/*.jpg`, about 70 KB
each, built from these.

## Adding or changing an image

```
python art-source/build-art.py
```

That rebuilds every `.jpg` from every `.png` here, and **fails if any of them
cannot be read over**. Do not hand-convert one and drop it in.

The check is the reason the script exists. `--rf-art-tint` is `0.65`, which is
the lowest alpha at which the page title and subtitle both clear 4.5:1 across
the current set, and it clears it by about 1%. There is no room for an image
that happens to be brighter than the ones already here, and the failure is not
something you would notice by looking, because it is a small run of bright
pixels behind one line of text rather than the whole band.

If a new image fails, either darken the master or raise `--rf-art-tint` in
`ravenfall-tokens.css` and `TINT` in the script together. Raising the tint makes
every other image darker too, so darkening the one master is usually the better
answer.

## Which page uses which

The mapping lives in `Shared/DashboardLayout.razor`. Scenes are matched to what
a page is about and shared along the nav groups, so a page you have not opened
before feels like somewhere and two pages you switch between do not look like
the same place. Anything unmapped falls back to `home`.

## Where the numbers came from

- The band is `cover` at `center 30%`, so the script crops to exactly that
  geometry. Nothing ships that is never drawn, which is most of the file size.
- Blur is 1.2px and is **not** for legibility: measured across 0 to 2.5px, the
  worst pixel behind the title moved 2.27 to 2.31. The tint does all of that
  work. Blur is for softness and because it roughly halves the encoded size,
  and pre-blurring keeps `filter: blur()` off a 420px-tall element on scroll.
- 1200px wide covers a wide `.main`; larger buys nothing visible through the
  blur.
