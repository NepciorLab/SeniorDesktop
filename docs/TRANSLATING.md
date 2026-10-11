# Translating SeniorDesktop

SeniorDesktop uses the standard **.NET `.resx` resource format** for its translations, with the usual naming convention:

| File                | Language                         |
|---------------------|----------------------------------|
| `Strings.resx`      | Neutral / English (the reference and the fallback) |
| `Strings.pl.resx`   | Polish                           |
| `Strings.de.resx`   | German                           |
| `Strings.<code>.resx` | any other language, `<code>` = ISO 639-1 code (`fr`, `es`, …) or a culture such as `pt-BR` |

`.resx` files are plain XML. You can edit them with any text editor, with Visual Studio, or with a resource editor such as
[ResX Resource Manager](https://github.com/dotnet/ResXResourceManager).

Built-in languages are embedded in the program (`Source/Languages/`). Additional languages — or corrections to the built-in
ones — are picked up **at run time, without recompiling**, from these folders (later folders win, key by key):

1. `Languages\` next to `SeniorDesktop.exe`
2. `%APPDATA%\SeniorDesktop\Languages\` (Settings → language tab → *Open the Languages folder*)

Keys that are missing in a translation fall back to English, so a partial translation works.

## Adding a language (e.g. French)

1. Copy `Source/Languages/Strings.resx` to `Strings.fr.resx`.
2. Translate the `<value>` of every `<data>` element. **Do not change the `name` attributes.**
3. Fill in the four metadata keys at the top:

   | Key                | Meaning                                                          | Example |
   |--------------------|------------------------------------------------------------------|---------|
   | `Language.Name`    | Native name shown on the language tile                           | `Français` |
   | `Language.Flag`    | Flag code (see below)                                            | `fr` |
   | `Language.Culture` | .NET culture for dates and numbers                               | `fr-FR` |
   | `Language.Order`   | Position of the tile (lower = earlier, default 100)              | `40` |

4. Put the file in `%APPDATA%\SeniorDesktop\Languages\` and restart the program. The new tile appears on the language tab.
5. Optional: open a pull request that adds the file to `Source/Languages/` (the project file embeds every `Languages\*.resx`
   automatically).

### Flags

Built-in vector flags exist for `pl`, `gb` and `de`. For any other flag code, SeniorDesktop looks for an image called
`flag-<code>.png` (for example `flag-fr.png`, 3:2 aspect ratio) in the same `Languages` folders; if there is none, a neutral
tile showing the code is used.

## Rules for translators

* `{0}`, `{1}` … are placeholders filled in by the program. Keep them (the order may change) — for example
  `Could not start: {0}` where `{0}` is the name of the tile.
* `\n` inside a value means a line break.
* Strings containing `|` (the file dialog filters `Settings.Dialog.Filter*`) are *description|pattern* pairs.
  Translate the descriptions only, never the patterns (`*.exe;*.lnk`).
* `Main.Clock.TimeFormat` and `Main.Clock.DateFormat` are .NET date/time format strings (for example `d. MMMM yyyy`). Choose the
  customary format of your language.
* `Default.*` keys are used once, to create the sample tiles on the first run. After that they are ordinary user data.
* The comment next to many keys explains where the text appears.
* UI text should be short and friendly; the program is aimed at visually impaired and elderly users.

## Checking your translation

Start SeniorDesktop, open the settings (hold the ⚙ button for a second, or press **Ctrl+Shift+S**), open the language tab
(the one with the flags) and pick your language. The whole interface switches immediately, so you can walk through every tab.

> The Polish, English and German texts were written with the help of an AI assistant and have not been reviewed by
> professional translators. Corrections from native speakers are very welcome.
