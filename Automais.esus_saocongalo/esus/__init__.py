"""Laboratório de recon do e-SUS de São Gonçalo ("Novo Esus", saogoncalo.esusmais.com.br).

Mesmo método dos laboratórios `Automais.SISREG/`, `Automais.SER/` e `Automais.SERNIT/`:
medir contra o sistema real, nunca assumir. Aqui a plataforma é outra — um SPA Vue 3 que
fala JSON com DOIS backends (Node/GraphQL em :8001 e PHP legado em :9001) — então não há
HTML para raspar: o laboratório chama as mesmas APIs que o front chama.

SOMENTE LEITURA por padrão — toda chamada passa por uma trava (ver `client.py`).
"""
