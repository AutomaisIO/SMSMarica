# ADR-0082 — Localização do paciente no dia da viagem do TFD, compartilhada pelo app com o motorista

- **Status:** Aceito (23/10/2026)
- **Data:** 2026-10-23
- **Contexto módulo:** Transporte (TFD) — app do cidadão ([`SMSMais.cidadao.app`](../../SMSMais.cidadao.app/)
  e [`SMSMais.cidadao.pwa`](../../SMSMais.cidadao.pwa/)), servidor, tablet do veículo
  ([`SMSMais.agente.app`](../../SMSMais.agente.app/)) e painel
- **Relacionado:** [ADR-0017](./0017-otimizacao-rota-tfd.md) (rota e tempo real do TFD),
  [ADR-0070](./0070-push-do-app-do-cidadao.md) (push do app), [ADR-0078](./0078-entrada-cidadao-cpf-nascimento.md)
  (entrada no app), [`docs/modulos/tfd/navegacao-motorista.md`](../modulos/tfd/navegacao-motorista.md)
  (resgate de 19/10)

## Contexto

O motorista chega ao ponto de embarque pelo endereço do cadastro: a coordenada do plano da rota ou,
sem ela, o endereço geocodificado. Ele erra quando o endereço está incompleto ou é de uma rua sem número,
quando a pessoa espera na esquina ou em outro portão e, no **resgate**, quando o paciente sai por uma
porta qualquer de um hospital grande, fora de Maricá. Hoje o motorista só tem o telefone para achar o
paciente.

A decisão de produto (23/10/2026) é que o paciente **compartilhe a localização do celular no dia da
viagem, com o app aberto**, nos dois aplicativos (app nativo e PWA), e que a primeira publicação nas
lojas espere essa função.

O que já existe e pesa no desenho:

- a viagem do paciente num dia é a `SessaoDeTratamento` (o `SessaoId` identifica tudo, inclusive no
  tablet). O embarque **não muda o status**, só grava `HoraSaidaResidencia` (ida) ou `HoraSaidaUnidade`
  (resgate) (`ViagemService`);
- o tablet fala por `X-Dispositivo-Token` (um por veículo) e busca a rota do dia por polling; não usa
  SignalR;
- o hub SignalR `/hubs/rastreamento` aceitava qualquer JWT, inclusive o do cidadão, no grupo do painel.
  Foi corrigido no PR #78 (só a equipe com Consulta em Rastreamento). **A localização do paciente não
  passa por ele**;
- o app nativo não tem pacote nem permissão de localização; a Segurança dos dados da Play diz "sem
  localização".

## Decisão

1. **Quando o paciente pode compartilhar.** Só enquanto ele **espera um embarque** de uma viagem de
   **hoje** (fuso de Brasília), num de dois trechos:
   - **ida:** a sessão está numa rota de hoje (Planejada ou Em andamento) como coleta, não foi cancelada
     nem marcada como ausente, e ainda não embarcou (`HoraSaidaResidencia` vazio). A janela abre
     **2 h antes da busca prevista** ou quando a rota começa, o que vier primeiro;
   - **resgate:** o paciente já desceu na unidade (`HoraChegadaUnidade`) e ainda não embarcou na volta
     (`HoraSaidaUnidade` vazio).

   Fora disso o servidor recusa (`409 localizacao.fora_da_janela`) e o app para sozinho. Dentro do
   carro a posição do paciente não interessa: a do veículo já existe.

2. **Consentimento específico, a cada viagem.** O app explica antes de pedir a permissão do aparelho:
   para que serve (o motorista achar você), quem vê (o motorista daquela viagem), quando para (no
   embarque, ou quando você tocar em Parar) e que o app só envia com ele aberto. Depois do "Compartilhar",
   o sistema operacional pede a permissão. O aceite fica registrado na trilha de auditoria (sessão do
   cidadão, viagem, canal, hora), que é a prova exigida pela LGPD (art. 8º). Não serve aceite genérico
   no termo: a autorização vale para aquela viagem.

3. **Só com o app aberto.** Permissão "durante o uso" (Android `ACCESS_FINE_LOCATION`/`COARSE`; iOS
   *When In Use*; no PWA, `watchPosition`). **Sem localização em segundo plano**: ela exige declaração
   especial do Google, com vídeo, é muito recusada e não é proporcional ao uso. Enquanto compartilha,
   o app mostra uma faixa fixa ("Compartilhando sua localização com o motorista · Parar"). Minimizou o
   app, o envio para, e a faixa avisa para deixar o app aberto. Com permissão só aproximada (Android 12+),
   o app envia assim mesmo, com a precisão real.

4. **O servidor guarda só a última posição, e só durante a espera.** Uma linha por sessão (não há
   trajeto): latitude, longitude, precisão em metros, se o aparelho diz que é posição simulada, hora da
   captura e do recebimento. Os campos de posição são **apagados no embarque**, na ausência, no
   cancelamento, quando o paciente toca em Parar e, por uma rotina periódica, quando a janela fecha. O
   registro do aceite fica na auditoria, sem a coordenada. A posição **não vai para log**, nem para push,
   nem para o FHIR (é dado operacional do transporte, schema `smsmarica`).

5. **Quem vê.**
   - **o tablet do veículo da viagem**: só os passageiros das paradas da rota daquele veículo, hoje
     (coleta na ida; resgate marcado por aquele carro), por um endpoint próprio, consultado a cada 15 s
     na tela de navegação. O tablet mostra o pino do paciente no mapa e, no painel do passageiro,
     "compartilhando · há X min · ±Y m" (e "posição simulada", se for o caso). A posição fica **só em
     memória**: não entra no cache cifrado da rota;
   - **o painel**, para quem tem Consulta em Translados (`GET rotas/{id}/localizacao-pacientes`): só
     **se** o paciente está compartilhando, há quanto tempo e com que precisão, **sem a coordenada**. A central fala com o motorista pelo telefone,
     e o motorista tem o mapa. Mostrar a coordenada no painel fica para um ADR futuro, com auditoria de
     leitura;
   - **o cidadão**: o próprio estado ("compartilhando desde HH:mm").

6. **O contrato.**
   - cidadão (`/auth/paciente`, JWT do cidadão, `[ExigeConsentimento]`):
     - `GET localizacao` → a viagem em que ele pode compartilhar agora e o estado (ou 204);
     - `POST localizacao/{sessaoId}` → registra o aceite e abre o compartilhamento (idempotente);
     - `PUT localizacao/{sessaoId}` `{ latitude, longitude, precisaoM, capturadaEm, simulada }` → 204;
     - `DELETE localizacao/{sessaoId}` → para e apaga a posição;
   - tablet (`/rastreamento/dispositivos`, `X-Dispositivo-Token`):
     `GET localizacao-passageiros` → os passageiros da rota do veículo, hoje, que estão compartilhando.

7. **Travas** (revisão de segurança de 23/10 e pentest VULTUS):
   - **posse:** a sessão tem de ser do paciente do token. No tablet, a sessão tem de estar numa rota de
     hoje do veículo do token. Assim ninguém manda nem lê a posição de outra pessoa;
   - **sem personificação:** "Entrar como paciente" (Sandbox) e a sessão aberta pelo link do WhatsApp não
     compartilham, como as outras escritas do transporte;
   - **validação:** latitude e longitude no intervalo, (0,0) recusado, precisão até 5 km, captura no máximo
     5 min no passado e 1 min no futuro;
   - **limite** próprio por sessão do cidadão (um envio a cada 10 s, com folga); o app manda no máximo a
     cada 15 s ou 20 m;
   - **posição simulada:** o Android informa quando a posição vem de um app de GPS falso; vai para o
     tablet como aviso, não bloqueia;
   - **transporte:** só HTTPS (PR #79 no app). O hub de rastreamento não é usado (PR #78).

8. **Modo demonstração.** O `DemoAdapter` do app nativo simula uma viagem de hoje na janela e aceita
   os envios sem rede, para a revisão das lojas ver a explicação e o pedido de permissão. O PWA não tem
   modo demonstração: lá a função só aparece com uma viagem de verdade na janela.

9. **O PWA não pode parar o que já funciona.** A mudança no PWA é só aditiva: nenhum fluxo existente
   muda. O cartão e a faixa ficam dentro de uma proteção de erro própria e somem em silêncio se o servidor
   não tiver os endpoints (404), se der erro ou limite, se o navegador não tiver GPS ou se a permissão for
   negada. A consulta só roda com a página visível e para na sessão se o endpoint não existir. O deploy vai
   na ordem servidor → PWA.

## O que muda em cada lugar

| Onde | Mudança |
|---|---|
| Servidor | entidade e migration (`translado_compartilhamento_localizacao`); `CompartilhamentoLocalizacaoService` com a regra da espera (o resgate reusa `ResgateService.EstaNaUnidade`, a mesma regra da lista de resgate); endpoints do cidadão, do tablet e do painel; limite `localizacao-cidadao` (12/min por sessão do app); encerramento na própria marcação do tablet (`ViagemService`, mesmo `SaveChanges`); `EncerramentoLocalizacaoWorker` (a cada minuto, para cancelamento, outro dia e já em casa); testes |
| App nativo | `geolocator`, permissões (Android e `Info.plist`), cartão no Transporte, explicação, faixa, envio, demonstração, versão 1.0.2 |
| PWA | o mesmo cartão e a faixa com `navigator.geolocation`, `X-App-Canal: pwa` |
| Tablet | consulta a cada 15 s na navegação, pino no mapa, aviso no painel do passageiro |
| Painel | "compartilhando · há X min" no translado (sem coordenada); artigo do Manual |
| Play | Segurança dos dados ganha "Local exato" (coletado, não compartilhado, opcional, funcionalidade); "Detalhes do login" ganha o passo da demonstração |
| Páginas públicas | Política: retenção da posição (só a última, apagada no embarque). Termos §1: compartilhar a localização no dia da viagem, se quiser |
| Docs | este ADR; `navegacao-motorista.md`; README do app |

## Consequências

- O motorista acha o paciente pelo pino, com a idade e a precisão da posição. Sem o app aberto, sem
  aceite ou sem sinal, continua tudo como hoje (endereço e telefone).
- Depende de o paciente abrir o app no dia. Um push "o carro está a caminho, abra o app para
  compartilhar sua localização" (sem dado de saúde, ADR-0070) fica como evolução.
- O tablet só mostra o pino depois de receber a versão nova.
- Nenhuma coordenada fica guardada depois da viagem. Se surgir a necessidade de histórico (por exemplo,
  para auditar uma reclamação de "não passaram aqui"), ela precisa de um ADR novo, com prazo de
  retenção e base legal.
- Testes só com simulação: banco e servidor locais, pacientes fictícios, GPS simulado no emulador ou no
  navegador. Nada com paciente real.
