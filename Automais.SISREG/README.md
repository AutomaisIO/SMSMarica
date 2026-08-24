# Automais.SISREG — laboratório de integração com o SISREG III

Sandbox **isolado** (Python) para mapear e documentar a integração com o
`sisregiii.saude.gov.br` (login, sessão, menus, endpoints) **antes** de portar o
motor validado para o `SMSMais.server` (.NET). Isolado de propósito: dá pra
testar/iterar sem depender de deploy.

## Setup

```bash
cd Automais.SISREG
python -m venv .venv && .venv\Scripts\activate   # opcional
pip install -r requirements.txt
cp .env.example .env    # preencha SISREG_USUARIO / SISREG_SENHA (credencial real)
python login_test.py    # testa login e salva capturas/pos_login.html
```

## Estrutura

- `sisreg/client.py` — motor HTTP (`SisregClient`): priming + login + sessão.
- `login_test.py` — script de teste do login e dump da tela pós-login.
- `docs/APRENDIZADOS.md` — **documentação viva** do que descobrimos do SISREG.
- `.env` / `capturas/` — **gitignored** (credencial e respostas com PII).

## Regras

- **Produção real.** Só leitura/navegação nesta fase; nada de escrever no SISREG
  sem OK explícito.
- Segredos só no `.env`. Nunca `git add` de `.env` ou `capturas/`.
