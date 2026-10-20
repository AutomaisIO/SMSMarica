# colaborativo-bernardo-rafael-auditar

Caixa de entrada para o **Bernardo auditar** o que o Rafael preparou com o Claude **sem acesso ao
build, ao servidor nem ao banco**. Cada item é uma subpasta `AAAA-MM-DD-assunto/` com:

- `README.md` — o problema, a decisão tomada, o que muda, o roteiro e as instruções para o Claude
  de quem for aplicar;
- `*.patch` — as mudanças de código, **ainda não aplicadas** (gerado com `git diff` contra a
  `main` indicada no README).

Nada nesta pasta é compilado nem publicado: os workflows de deploy filtram por pasta e não olham
para cá. Para aplicar, siga o README do item. Depois de commitado, o item pode sair daqui (ou ficar
como registro, a critério do Bernardo).

## Itens

| Item | Situação |
|---|---|
| [`2026-10-20-holter-mapa-satelite`](./2026-10-20-holter-mapa-satelite/README.md) | Auditado e aplicado em 20/10/2026 (`feat/holter-mapa-satelite`) — ver "Decisões do Bernardo" no item |
