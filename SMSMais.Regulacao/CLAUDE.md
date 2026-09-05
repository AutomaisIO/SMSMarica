# SMSMais.Regulacao — instruções para quem trabalha nesta pasta

Esta pasta é o **planejamento e a memória de execução** do módulo Regulação → Solicitações. As regras do `CLAUDE.md` da raiz continuam valendo; estas são adicionais.

1. **Antes de qualquer tarefa, leia `PROGRESSO.md`.** Ele diz o incremento em andamento e a próxima tarefa. Não pule incrementos.
2. **Leia o plano inteiro** (`NN-*.md`) antes de tocar em código. Os planos citam os arquivos existentes a reaproveitar; não reinvente o que eles apontam.
3. **Uma tarefa por vez**, inteira e verificada (build 0 erros / 0 warnings, teste, tela). Depois atualize `PROGRESSO.md`: checkbox, diário, aprendizados.
4. **Plano errado → corrija o plano e registre em "Desvios do plano".** Nunca implemente em silêncio algo diferente do escrito.
5. **Produção só com OK explícito do Bernardo**, registrado em `PROGRESSO.md` (tabela "OKs de produção"). Isso inclui deploy, migration em prod e qualquer escrita em SISREG, SER ou SERNIT reais. Os spikes a e b são escrita real.
6. **SISREG: nunca depurar clicando de novo.** O orçamento anti-robô é do operador; ~700 requisições travam a credencial por 24h. Capture tudo na primeira tentativa.
7. Decisões D-1 a D-9 (README §5) não se rediscutem sem falar com o Bernardo. Se uma delas ficar impossível na prática, pare e pergunte.
8. Documentação em pt-BR; só `.md` nesta pasta (sem `.html`).
9. Código novo do módulo: backend em `SMSMais.Core/Regulacao/`, entidades em `SMSMais.Data/Entities/Regulacao/`, front em `SMSMais.front/src/features/regulacao/`. Não crie a terceira cópia de `features/ser` — extraia o comum para `shared/regulacao/`.
