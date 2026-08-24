# SMSMais.front

Painel web do **SMS Maricá**. React + Vite + TypeScript consumindo `SMSMais.server`.

## O que é

Aplicação web administrativa e operacional:

- **Operador:** foco em execução (cadastros, alocações, conferências).
- **Gestor:** foco em visão gerencial e relatórios (a partir do marco M6).

Consome exclusivamente a API do [`SMSMais.server`](../SMSMais.server/README.md). Os aplicativos Flutter (cidadão e agente) atendem públicos distintos e não substituem este painel.

## Stack

- Vite + React 18 + TypeScript (`strict`)
- TanStack Query + Axios (estado servidor e cliente HTTP)
- react-router-dom v6
- Zustand (auth mock local)
- Zod (validação de formulários)
- Tailwind CSS com tema Maricá (vermelho `#C8102E` + branco)

Decisões e convenções em [`../docs/conventions.md §4`](../docs/conventions.md).

## Como rodar

```bash
npm install
npm run dev        # http://localhost:5173
```

O Vite faz proxy de `/api/*` para `http://localhost:5080` (backend local).
Para apontar para outra URL, copie `.env.example` para `.env.local` e ajuste `VITE_API_BASE_URL`.

Com o `SMSMais.server` rodando (`dotnet run --project SMSMais.server/src/SMSMais.Api`), faça login (mock) e acesse **Operador → Pacientes** para cadastrar e consultar pacientes contra o backend real.

## Estrutura

```
src/
├── app/
│   ├── layout/              (ShellAutenticado: header + nav por perfil)
│   ├── pages/               (landing pages por perfil + 404)
│   ├── providers/           (QueryProvider)
│   └── router/              (AppRouter, RotaProtegida)
├── features/
│   ├── auth/pages/          (LoginPage mock)
│   └── pacientes/
│       ├── api/             (cliente HTTP + queries TanStack)
│       ├── components/      (formulário + detalhe)
│       ├── pages/           (PacientesPage)
│       ├── schemas/         (zod)
│       └── types.ts
└── shared/
    ├── api/                 (axios + tratamento de erro)
    ├── auth/                (authStore mock — trocar no S2.4)
    ├── lib/                 (cn utility)
    └── ui/                  (Button, Input, Campo, theme)
```

## Autenticação

Mock local (Zustand + localStorage) até a entrega **S2.4 Identidade** no server expor JWT real. Ao entrar, escolha o perfil; o roteador direciona para `/operador/*` ou `/gestor/*`.

## Scripts

```bash
npm run dev        # servidor de desenvolvimento
npm run build      # build de produção (typecheck + bundle)
npm run preview    # serve o build
```
