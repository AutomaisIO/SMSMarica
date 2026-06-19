# SMSMarica.cidadao.pwa

App do Paciente (PWA) do SMS Maricá — **FT7** do módulo TFD. React + Vite + TypeScript,
instalável (vite-plugin-pwa). Login por **CPF + código (OTP) no WhatsApp**, sem senha.

Publicado em **https://app.smsmarica.online** (mesmo server do `SMSMarica.front`; vhost nginx
servindo `/var/www/smsmarica-app`, deploy via `.github/workflows/deploy-app.yml`).

## Telas (Fase 1)

- **Login** (`/login`) → informa CPF, recebe OTP no WhatsApp.
- **Código** (`/login/codigo`) → valida o OTP, recebe o token de sessão.
- **Agenda** (`/`) → lista os transportes/translados do paciente.
- **Translado** (`/translado/:id`) → detalhe, **ETA** (chegada do veículo) e
  **confirmar acompanhante**.

## Páginas legais (estáticas, crawláveis)

`public/privacidade/index.html` e `public/termos/index.html` são HTML estático servido
diretamente pelo nginx (não passam pelo roteamento da SPA). São as URLs apontadas no cadastro
dos apps Google / Microsoft / Meta:

- https://app.smsmarica.online/privacidade/
- https://app.smsmarica.online/termos/

## Backend (pendente)

Os endpoints de paciente ainda **não existem** no `SMSMarica.server` (marcados com `TODO(FT7)`):

- `POST /auth/paciente/solicitar-otp` `{ cpf }`
- `POST /auth/paciente/validar-otp` `{ cpf, codigo }` → `{ token, paciente }`
- `GET  /auth/paciente/meus-translados`
- `GET  /auth/paciente/translados/{id}`
- `POST /auth/paciente/translados/{id}/acompanhante` `{ confirmado }`

## Comandos

```bash
npm install
npm run dev       # Vite (http://localhost:5174), proxy /api -> localhost:5080
npm run build     # tsc -b && vite build  (gera dist/ instalável)
npm run preview
```
