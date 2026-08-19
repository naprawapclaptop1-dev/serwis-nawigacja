# NAVI PRO — automatyczne aktualizacje

Workflow `.github/workflows/deploy-navi-pro.yml` wdraża NAVI PRO do Cloudflare po każdym `push` do `main`.

## Jednorazowa konfiguracja sekretów GitHub

W repozytorium:
`Settings → Secrets and variables → Actions → New repository secret`

dodaj:

- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

Hasła/klucza API nie wpisuj do kodu ani do plików projektu.

## Co dzieje się później

1. Zmieniasz pliki NAVI PRO.
2. Robisz `git push` do `main`.
3. GitHub Actions uruchamia Wrangler.
4. Nowa wersja trafia automatycznie do Cloudflare.
5. Service Worker NAVI PRO wykrywa nową wersję i może pokazać użytkownikowi przycisk aktualizacji.
