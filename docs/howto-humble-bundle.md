# Importing Humble Bundle e-books

Humble Bundle has **no official API** for downloading purchased e-books. Community projects use undocumented endpoints behind the logged-in session cookie (`_simpleauth_sess`): `/api/v1/user/order` lists the order keys, `/api/v1/order/{gamekey}` returns the files with their download links. These endpoints are not supported by Humble Bundle, were last described in 2017–2022 and can change at any time. shelf therefore contains no Humble code.

Workflow:

1. Download the books with a tool of your choice, for example the bulk download on the Humble Bundle library page, or a community tool such as `humble-cli`. Check Humble Bundle's terms of use first.
2. Put the files in one folder, e.g. `~/Downloads/humble`.
3. `shelf import ~/Downloads/humble --dry-run`, then `shelf import ~/Downloads/humble`.

Humble Bundle e-books are DRM-free. shelf does not remove DRM (see the PRD, non-goals).

Sources: <https://www.schiff.io/projects/humble-bundle-api/>, <https://github.com/guitargeek/humblebundle-backup>.
