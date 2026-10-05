## AutoResxTranslator (Fork)

This project is a fork of the original AutoResxTranslator repository.  
All credits for the original idea, implementation, and base functionality go to the original author: https://github.com/salarcode/AutoResxTranslator

This fork focuses on improving usability, adding enhancements, and making the tool more practical for everyday use.

---

## About

A tool to automatically translate Resx files to any language using Google Translator.  
No API key is required for the translator.  
This tool also provides a text translator out of the box.

---

## Text translation
------
![Text-Translation](/Wiki/Images/text-translation.png?raw=true)

---

## Resx Translation
------
![ResX-Translation](/Wiki/Images/resx-translation.png?raw=true)

## Labman manual translation

Use the **Manual Labman** tab to select the manual repository's `localization/es.json`.
The language table uses the same languages as RESX. Loading the source automatically
checks languages with an existing `<language>.json` in that directory; you can check or
uncheck any destination. Configure Google, Microsoft or DeepL in **Translate Service**,
adjust the terms to preserve (one per line), and click **Traducir manual**. Only checked
languages and their histories are updated; missing destination files are created.
Spanish remains read-only. Provider language support still applies.

The translator records the exact Spanish SHA-256 per key/language in
`localization/.translation-history/<language>.json`. Keep this persistent directory with
the catalogs (in version control or a backup). Without history, existing translations
are translated again. Removed keys are deleted, and failed or cancelled pending values
remain empty so Docusaurus can fall back to the current Spanish. Completed results are
saved atomically after each key. Only one translation run can write the catalogs at a time.

Markdown syntax, links, images, Docusaurus admonitions, anchors, code and placeholders
are protected. Articles are sent whole when they fit; larger articles are split at safe
block boundaries and reassembled into one JSON value. An indivisible block above the
provider limit or a damaged marker stays pending and is reported in the tab and app log.
Google uses a conservative URL limit; Microsoft/DeepL accept larger blocks. The tab uses
automatic translation; review the resulting catalogs before publication.

After translation, run these commands **in the manual repository**, not this application:

```powershell
npm.cmd run translations:generate
npm.cmd run translations:check
npm.cmd run typecheck
npm.cmd run build
npm.cmd run translations:strict # Required for a completely translated release
```

Offline regression checks (after building the application):

```powershell
powershell -ExecutionPolicy Bypass -File tests/ManualTranslationChecks.ps1
```

---

## Resx translation is in progress
------
![ResX-Translation](/Wiki/Images/resx-translating.png?raw=true)

---

## Import from Excel sheet
------
![excel-import](/Wiki/Images/excel-import.png?raw=true)

---

## Select Microsoft Cognitive Translation Service
------
![ms-translator](/Wiki/Images/ms-translator.png?raw=true)

---

## Credits

- Original project: https://github.com/salarcode/AutoResxTranslator  
- This repository is a fork with improvements and usability enhancements.
