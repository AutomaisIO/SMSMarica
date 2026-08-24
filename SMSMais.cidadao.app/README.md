# SMSMais.cidadao.app

## O que é

Aplicativo **Flutter** destinado ao **cidadão / paciente** (e uso associado ao acompanhante quando aplicável).

## Para que serve

- Permitir que o usuário **acesse e complete** seu cadastro junto ao programa.
- Exibir **agendamentos de translado** ligados aos tratamentos (quando a funcionalidade estiver disponível na API).
- **Confirmar disponibilidade** para ser buscado no endereço cadastrado.
- Após **alocação em veículo**: mostrar o **assento** (de preferência com representação visual) e **tempos estimados de chegada** do veículo, de forma semelhante a apps de mobilidade urbana — tanto na **ida** quanto na **volta**.
- Permitir **avaliar o motorista** e enviar **sugestões ou críticas** sobre o serviço.

## Integrações previstas

- **WhatsApp** (notificações ou fluxos de contato — conforme definido na implementação e nas políticas do município).

## Relação com o restante do ecossistema

Consome a API do **`SMSMais.server`**. Cadastros e parametrizações feitas pelas equipes ocorrem no **`SMSMais.front`**; o motorista opera pelo **`SMSMais.agente.app`**.
