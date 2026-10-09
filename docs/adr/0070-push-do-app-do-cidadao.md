# ADR-0070 — Notificação (push) no app do cidadão

**Status:** aceito · **Data:** 2026-10-09 · implementado em 09/10/2026 (não deployado; o app nativo ainda não está nas lojas)
**Relacionado:** [ADR-0018](./0018-identidade-e-sessao-do-cidadao.md) (sessão do cidadão) ·
[ADR-0043](./0043-instancia-por-municipio.md) (configuração por instância) ·
[ADR-0057](./0057-destinatario-correto-e-contato-negado.md) (destinatário correto) ·
`SMSMais.cidadao.app/README.md` (como configurar o Firebase no app)

## Contexto

O App do Cidadão ganhou a versão nativa (`SMSMais.cidadao.app`, Flutter, espelho do PWA). O Bernardo
pediu um caminho para **mandar uma mensagem do servidor direto para o app de um paciente específico**.
Até aqui, tudo o que chega ao cidadão vai pelo WhatsApp (templates da Meta, janela de 24h, custo por
conversa). O app instalado permite um canal próprio, sem template e sem custo por mensagem.

Três fatos do código moldaram a decisão:

- **O login do cidadão aceita várias sessões ao mesmo tempo.** `CidadaoSessaoService.AbrirSessaoAsync`
  não revoga as anteriores: o paciente pode estar no app, no PWA e no navegador ao mesmo tempo. O
  "aparelho" que recebe o push, portanto, não é o paciente — é a **sessão**.
- **Já existe um cofre cifrado de credenciais de integração** (`smsmarica.integracao_credencial`, tela
  Integrações), com segredo write-only, auditoria e liga/desliga.
- **Não existe push nenhum** hoje (nem Web Push no PWA).

## Decisão

1. **Transporte: Firebase Cloud Messaging, API HTTP v1.** É o único caminho de push para Android com
   Google Play services e cobre o iOS (APNs) pelo mesmo envio. Um POST por aparelho, sequencial.
2. **O token do aparelho mora na sessão** (`cidadao_sessao.push_token`, `push_plataforma`,
   `push_registrado_em`). O envio vai a **todas as sessões ativas** do paciente que têm token
   (`revogada_em IS NULL`, `expira_em > agora`, acesso ativo). Consequências diretas:
   - logout zera o token junto com a revogação — o celular que saiu não recebe mais nada;
   - sessão expirada ou revogada por qualquer motivo nunca recebe (o filtro do envio é a fonte da
     verdade; não é preciso limpar o token em cada ponto que revoga);
   - o mesmo token não fica em duas sessões: ao registrar, ele é retirado de qualquer outra;
   - o envio só apaga o token quando o FCM diz que ele morreu (`UNREGISTERED`, ou `INVALID_ARGUMENT` no
     campo `token`), como manda a documentação dele; `SENDER_ID_MISMATCH` (app gerado com um projeto do
     Firebase e chave em Integrações de outro) é tratado como erro de configuração — vai para o log como
     erro e **não** apaga o token.
3. **Sem dado de saúde no push.** O texto aparece na tela do celular, inclusive bloqueado, e passa pelos
   servidores do Google sem criptografia ponta a ponta. O push **chama a pessoa para abrir o app**; o
   conteúdo clínico continua atrás do login. O `data` leva só a rota a abrir e o id da notificação. O
   painel mostra esse aviso a quem escreve.
4. **Destino é lista fixa.** A notificação pode abrir uma das telas internas do app (Início, Consultas e
   Exames agendados, Atendimentos, Exames, Documentos, Transporte, Chat, Perfil). Servidor, painel e app
   validam a mesma lista — nada de URL livre.
5. **Histórico de envio** em `smsmarica.cidadao_notificacao`: quem enviou, quando, para qual paciente,
   título, mensagem, destino, quantos aparelhos e quantos o Firebase aceitou. Serve à auditoria (LGPD) e
   ao "não chegou". Entra no repontador da unificação de cadastros.
6. **Credencial no cofre de Integrações**, provedor `fcm`: o JSON da conta de serviço do Google é o
   segredo (cifrado, nunca devolvido); o `projectId` fica público na tela; `Ativo` desliga o envio.
   Troca sem SSH e sem deploy, uma por instância (ADR-0043). Botão **Testar** pede um token de acesso
   novo **e** faz um envio de validação (`validate_only`, para um tópico — não chega a ninguém): só o
   envio prova que a API "Firebase Cloud Messaging (V1)" está ligada no projeto e que a conta tem papel
   de envio. Credencial que não decifra neste servidor (cifrada em outro ambiente) responde no Testar e
   barra o envio com `push.credencial_ilegivel`, em vez de erro 500.
7. **Sem pacote do Google no servidor.** O token OAuth2 da conta de serviço é obtido à mão (JWT RS256 com
   `System.Security.Cryptography`, escopo só `firebase.messaging`, cache até 5 min antes de vencer). O
   `FirebaseAdmin` traz estado global e escopos largos; o `Google.Apis.Auth` marcou obsoleta a leitura
   de JSON que usaríamos.
8. **Permissão própria: `NotificacaoAppCidadao = 81`.** `Consulta` vê os aparelhos e o histórico na ficha
   do paciente; `Edicao` envia. Mesmo critério do `PesquisaSatisfacao = 56`: consultar o paciente não dá
   direito de mandar mensagem a ele.
9. **No app, o Firebase é configurado só pelo Dart** (`--dart-define-from-file=firebase.json`, arquivo fora
   do git), sem `google-services.json` nem `GoogleService-Info.plist`. Sem essa configuração — e sempre
   no modo demonstração e na web — o push fica desligado e o app funciona igual. O registro acontece
   depois do login **e** do termo aceito; a permissão de notificação é pedida nesse momento.

## Consequências

- O envio manual pelo painel é o primeiro uso. Avisos automáticos (exame agendado, laudo pronto,
  transporte) podem usar o mesmo serviço depois — `INotificadorExame` já é o ponto de extensão — mas
  **cada gatilho automático é decisão à parte**, para não notificar em dobro com o WhatsApp.
- Push não substitui o WhatsApp: só alcança quem instalou o app nativo, entrou e aceitou receber.
- O PWA continua sem push. Web Push (VAPID) pode vir depois com o mesmo modelo (token por sessão,
  `push_plataforma = web`).
- Para funcionar em produção falta configurar o projeto no Firebase (apps Android e iOS, chave APNs da
  Apple, conta de serviço) — passo a passo no README do app — e publicar o app.
- O FCM está trocando o "token de registro" pelo FID (Firebase Installation ID). O campo `token` da API
  aceita os dois durante a transição; quando o `firebase_messaging` publicar o `register()`, o app migra
  sem mudar o servidor.
