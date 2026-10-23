# SMSMais.cidadao.pwa

**App do Cidadão** (PWA) do SMS Maricá — a saúde digital do município no bolso do cidadão.
React + Vite + TypeScript, instalável (vite-plugin-pwa). Login por **CPF + data de nascimento +
código (OTP) no WhatsApp**, sem senha.

> **Escopo:** o app **não** é específico de TFD. O transporte/TFD é apenas **um módulo**.
> O app do cidadão evolui para reunir os serviços digitais de saúde de Maricá.

Publicado em **https://app.smsmarica.online** (mesmo server do `SMSMais.front`; vhost nginx
servindo `/var/www/smsmarica-app`, deploy via `.github/workflows/deploy-app.yml`).

## Módulos

**Fase 1 (este scaffold) — Transporte / TFD (FT7):**

- **Login** (`/login`) → informa CPF e data de nascimento ([ADR-0078](../docs/adr/0078-entrada-cidadao-cpf-nascimento.md)).
- **Telefone** (`/login/telefone`) → escolhe o seu WhatsApp entre os números que a Saúde já
  conhece (só o final aparece); o código nunca vai para um número digitado. "Nenhum desses é meu"
  procura no e-SUS (se ligado para o canal, em Sistema → App do cidadão) ou orienta ir ao posto.
- **Código** (`/login/codigo`) → valida o OTP, recebe o token de sessão.
- **Agenda** (`/`) → lista os transportes/translados do cidadão.
- **Translado** (`/translado/:id`) → detalhe, **ETA** (chegada do veículo) e
  **confirmar acompanhante**.

**Próximos módulos (roadmap do app do cidadão):**

- **Agendamento** — agendar, confirmar e cancelar **consultas e exames** pelo app
  (não só pelo WhatsApp).
- **Meus documentos de saúde** — **laudos de exames de imagem**, **boletins de atendimento
  médico**, **receituário** e **cópias de exames de imagem**.

## Páginas legais (estáticas, crawláveis)

`public/privacidade/index.html` e `public/termos/index.html` são HTML estático servido
diretamente pelo nginx (não passam pelo roteamento da SPA). São as URLs apontadas no cadastro
dos apps Google / Microsoft / Meta:

- https://app.smsmarica.online/privacidade/
- https://app.smsmarica.online/termos/

Cobrem dados sensíveis de saúde (laudos, exames, receituário), agendamento e transporte.
Canal de contato/Encarregado (LGPD): **contato@smsmarica.online**.

## Backend

Toda chamada leva o cabeçalho `X-App-Canal: pwa` (o app nativo manda `nativo`): é por ele que o
servidor sabe se a busca do telefone no e-SUS está ligada para este canal.

Login (`/auth/paciente`, rotas públicas — ADR-0078):

- `POST entrada` `{ cpf, dataNascimento: "aaaa-mm-dd" }` → `{ desafioId, opcoes: [{ id, final, origem, verificado }], podeBuscarEsus, validadeSegundos }`
- `POST entrada/{desafioId}/esus` → a mesma resposta, com os números achados no e-SUS
- `POST entrada/{desafioId}/enviar` `{ opcaoId }` → `{ codigoTeste, telefoneMascarado, … }`
- `POST validar-otp` `{ cpf, codigo }` → `{ token, paciente }`

As antigas `solicitar-otp` e `solicitar-otp-verificacao` respondem 410 (`entrada.atualizar`). As
demais rotas do cidadão estão em `src/lib/api.ts`.

Localização no dia da viagem (`/auth/paciente/localizacao`, [ADR-0079](../docs/adr/0079-localizacao-do-paciente-no-transporte.md)):

- `GET localizacao` → o embarque que o paciente espera agora (a ida, a partir de 2 h antes da
  busca, ou o resgate), ou 204 fora da espera. O app consulta a cada minuto, só com a página visível.
- `POST localizacao/{sessaoId}` → o aceite desta viagem, depois da explicação e antes do pedido de
  permissão do navegador.
- `PUT localizacao/{sessaoId}` `{ latitude, longitude, precisaoM, capturadaEm, simulada }` → a
  posição, no máximo a cada 15 s (antes, se andou 20 m; nunca antes de 10 s). 409 = a espera acabou
  (embarcou, cancelou): o GPS desliga.
- `DELETE localizacao/{sessaoId}` → "Parar" (o Sair também para).

A função é aditiva (§9 do ADR): o cartão e a faixa ficam dentro de `ProtecaoDeErro`, e qualquer
falha só esconde a função. Servidor sem as rotas (404): o app para de consultar até ser aberto de
novo, e o resto segue igual.

## Comandos

```bash
npm install
npm run dev       # Vite (http://localhost:5174), proxy /api -> localhost:5080
npm run build     # tsc -b && vite build  (gera dist/ instalável)
npm run preview
```
