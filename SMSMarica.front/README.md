# SMSMarica.front

## O que é

Aplicação web em **React** com **Vite** — painel administrativo e operacional do SMS Maricá.

## Para que serve

- Dar suporte ao trabalho diário do **operador**: cadastros, consultas, alocação de pacientes (e acompanhantes) em **assentos** do veículo conforme layout por fileiras, acompanhamento de demandas de translado geradas pela periodicidade dos tratamentos, entre outras rotinas operacionais.
- Oferecer ao **gestor** uma visão mais ampla: **dashboards**, indicadores e informações agregadas para acompanhamento da operação.

## Perfis

- **Operador:** foco em execução (lista do dia, alocações, conferências).
- **Gestor:** foco em visão gerencial e relatórios (evolução conforme requisitos).

## Relação com o restante do ecossistema

Consome exclusivamente a API do **`SMSMarica.server`**. Os aplicativos móveis (**cidadao** e **agente**) não substituem este painel: cada canal tem público e função distintos.
