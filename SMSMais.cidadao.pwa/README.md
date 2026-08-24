# SMSMais.cidadao.pwa

**App do Cidadão** (PWA) do SMS Maricá — a saúde digital do município no bolso do cidadão.
React + Vite + TypeScript, instalável (vite-plugin-pwa). Login por **CPF + código (OTP) no
WhatsApp**, sem senha.

> **Escopo:** o app **não** é específico de TFD. O transporte/TFD é apenas **um módulo**.
> O app do cidadão evolui para reunir os serviços digitais de saúde de Maricá.

Publicado em **https://app.smsmarica.online** (mesmo server do `SMSMais.front`; vhost nginx
servindo `/var/www/smsmarica-app`, deploy via `.github/workflows/deploy-app.yml`).

## Módulos

**Fase 1 (este scaffold) — Transporte / TFD (FT7):**

- **Login** (`/login`) → informa CPF, recebe OTP no WhatsApp.
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

## Backend (pendente)

Os endpoints de cidadão/paciente ainda **não existem** no `SMSMarica.server` (marcados com
`TODO(FT7)`):

- `POST /auth/paciente/solicitar-otp` `{ cpf }`
- `POST /auth/paciente/validar-otp` `{ cpf, codigo }` → `{ token, paciente }`
- `GET  /auth/paciente/meus-translados`
- `GET  /auth/paciente/translados/{id}`
- `POST /auth/paciente/translados/{id}/acompanhante` `{ confirmado }`

(Os módulos de agendamento e documentos de saúde terão seus próprios endpoints.)

## Comandos

```bash
npm install
npm run dev       # Vite (http://localhost:5174), proxy /api -> localhost:5080
npm run build     # tsc -b && vite build  (gera dist/ instalável)
npm run preview
```
