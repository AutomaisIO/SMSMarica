# SMSMarica.agente.app

## O que é

Aplicativo **Flutter** destinado ao **motorista** (agente de transporte sanitário).

## Para que serve

- Receber e seguir **rotas** e paradas do dia (pontos de embarque/desembarque).
- Exibir o **próximo** destino de forma clara durante o trajeto.
- Abrir apps de navegação (**Waze**, **Google Maps** ou outro compatível) via integração nativa no **Android**.
- Enviar a posição **GPS em intervalos** para o backend, permitindo rastreamento e estimativas de chegada para o cidadão.
- Usar **geofencing** para identificar quando o veículo **chega** perto de residências e dos locais de tratamento/unidades.

## Escopo de plataforma

**Somente Android** nesta fase (decisão de produto: integrações nativas de navegação e políticas de localização).

## Relação com o restante do ecossistema

Consome a API do projeto **`SMSMarica.server`**. O cadastro administrativo de motoristas, veículos e alocações é feito no **`SMSMarica.front`** (ou futuras integrações).
