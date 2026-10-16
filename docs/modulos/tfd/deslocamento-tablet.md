# Tablet do veículo — deslocamento e Mapa da frota

Contrato entre o app do motorista (`SMSMais.agente.app`), a API (`SMSMais.server`) e o painel
(`SMSMais.front`) para o tablet fixo no carro aparecer no **Mapa da frota** e medir o deslocamento.

## Ideia

- O tablet é um **dispositivo de rastreamento vinculado a um veículo** (não a um motorista).
- No painel, em **Veículos › editar › Tablet vinculado**, quem tem `Veiculos/Alteracao` gera um
  **código de ativação** (8 caracteres, vale 24 h). No tablet, tela **Vincular veículo**, digita o código
  uma vez → recebe um **token próprio** (guardado no `flutter_secure_storage`). Gerar novo código e
  ativar substitui o token anterior; **Desvincular** revoga.
- O tablet manda posições com o token no header `X-Dispositivo-Token`. Não usa JWT de usuário nem
  `X-API-Key` (que dá acesso pleno à API).
- O Mapa da frota mostra, além das rotas do dia, **todo veículo com tablet que mandou posição hoje**,
  com velocidade.

## API (`https://api.smsmarica.online`, sem prefixo `/api`)

### Tablet (anônimo + token do dispositivo)

`POST /rastreamento/dispositivos/ativar`
```json
// request
{ "codigo": "K7PX2M9Q", "modelo": "Qualcomm Device", "identificador": "ANDROID_ID" }
// 200
{ "token": "base64url", "veiculoId": "guid", "veiculoPlaca": "ABC1D23", "veiculoModelo": "Spin" }
```
404 código inválido/expirado/já usado.

`GET /rastreamento/dispositivos/eu` (header `X-Dispositivo-Token`) → `{ veiculoId, veiculoPlaca, veiculoModelo }`. 401 = token revogado.

`POST /rastreamento/dispositivos/pontos` (header `X-Dispositivo-Token`)
```json
{ "pontos": [
  { "latitude": -22.91, "longitude": -42.82, "velocidadeKmh": 43.2, "rumo": 182.0,
    "precisaoM": 6.0, "capturadoEm": "2026-10-08T12:00:00Z" } ] }
```
→ 204. Até 500 pontos por lote. `capturadoEm` em UTC, no máximo +5 min no futuro. 401 = token revogado.

### Painel (JWT, módulo `Veiculos`)

| Método | Rota | Uso |
|---|---|---|
| GET | `/veiculos/{id}/dispositivo` | `{ id, ativo, ativadoEm, ultimoContatoEm, modelo, codigoPendente, codigoExpiraEm }` ou 204 |
| POST | `/veiculos/{id}/dispositivo/codigo` | gera código de ativação → `{ codigo, expiraEm }` |
| DELETE | `/veiculos/{id}/dispositivo` | desvincula (revoga o token) |

### Mapa da frota

`GET /rastreamento/frota?data=` ganha itens de veículos com tablet e os campos novos em cada item:
`velocidadeKmh`, `rumo`, `origem` (`"rota"` | `"tablet"`). Item só-tablet vem com `rotaId = null`,
`status = null`, motorista vazio.

## App — tela Deslocamento

- Primeira tela depois da entrada do motorista (rota `/deslocamento`); Rota do dia continua acessível.
- Mapa Google (`GoogleMapsMapView` do `google_navigation_flutter`, sem sessão de navegação — não
  cobra como navegação) com "minha localização" e câmera seguindo.
- **Sem botão Iniciar/Parar** (desde 15/10/2026): o contador de distância e tempo começa sozinho ao
  iniciar a navegação do translado e para no fim. A tela mostra "Aguardando translado", "Translado em
  andamento" ou "Translado concluído" e a velocidade ao vivo.
- Filtro de GPS: ignora fix com precisão > 30 m, saltos que implicariam > 180 km/h e passos menores que
  a precisão quando parado (evita o odômetro "andar" com o carro parado).
- **GPS nativo do Android** (`forceLocationManager`): os tablets da frota (Qualcomm msm8953, Android 9)
  não têm Play Services funcionando, e o caminho do Play Services derrubava o app. Nunca chamar
  `Geolocator.isLocationServiceEnabled()` (bug do geolocator_android 4.6.2).
- Envio ao servidor (se vinculado), **pelo movimento**: a cada 5 s andando (acima de 5 km/h) e a cada
  60 s parado (depois de 30 s abaixo disso). Sem rede, a fila fica **em disco cifrado** (SQLCipher,
  `posicoes.db`, até 10 000 pontos) e sai em lotes de 500. Trocou o veículo do vínculo, a fila do
  anterior é descartada.

## REPORT — o motorista avisa a central

Botão vermelho na entrada, no Deslocamento, na Rota do dia e na navegação: acidente, pane no veículo,
paciente passou mal ou outro (com texto). O aviso é **gravado no tablet antes de enviar** e reenviado a
cada 15 s; o `idLocal` (UUID do tablet) impede a central de receber duas vezes. Quando não chega, a tela
diz o motivo real — sem rede, central com falha (5xx) ou central que não aceitou (outro 4xx, com o
código) — e sempre "Se for urgente, ligue para a central". Um aviso recusado não trava os de trás.

`POST /rastreamento/dispositivos/ocorrencias` (header `X-Dispositivo-Token`)
```json
{ "idLocal": "uuid", "tipo": "Acidente", "descricao": null, "latitude": -22.91, "longitude": -42.82,
  "precisaoM": 9, "ocorridaEm": "2026-10-15T12:00:00Z" }
```
→ 201 `{ id, recebidaEm }`. `tipo`: `Acidente` | `PaneVeiculo` | `PacienteMalEstar` | `Outro` (este
exige `descricao`). Posição opcional. Painel: tela **Ocorrências** (faixa vermelha em tempo real pelo
hub `/hubs/rastreamento`, Atender/Encerrar) — ver o manual do painel.

## Entrada do motorista no tablet (ADR-0073)

O tablet continua falando só pelo token do veículo; quem está dirigindo é conferido **pelo rosto, no
próprio tablet** ([ADR-0073](../../adr/0073-login-facial-motorista-tablet.md)). Só entra o motorista
**escalado hoje** (rota do dia, fuso de Brasília) naquele veículo:

- **Entrar pelo rosto** — o tablet baixa a foto de referência dos escalados e gera o vetor aqui;
  compara depois da prova de vida (piscar, virar para o lado sorteado, voltar de frente). Sem rede,
  usa a escala guardada do dia.
- **Primeiro acesso** — CPF → código de 6 dígitos no WhatsApp do cadastro → termo de ciência →
  captura → a foto vira a referência (os outros tablets baixam a mesma).
- **Código da central** — 6 números gerados no painel (Veículos › Tablet vinculado), 15 min, uso único.
- Antes de abrir a navegação de um translado, o tablet confere de novo o motorista da sessão.
- Cada conferência (aprovada ou não, com selfie de 320 px) vai para uma fila cifrada e é registrada no
  servidor; a selfie é apagada em 90 dias.

| Método | Rota (header `X-Dispositivo-Token`) | Uso |
|---|---|---|
| GET | `/rastreamento/dispositivos/motoristas-do-dia` | `[{ motoristaId, nome, rotaId, temReferencia, fotoHash, referenciaCadastradaEm }]` |
| GET | `/rastreamento/dispositivos/motoristas/{id}/referencia-facial` | `{ fotoBase64, fotoHash, cadastradaEm }` — só de escalado |
| POST | `/rastreamento/dispositivos/primeiro-acesso/codigo` | `{ cpf }` → `{ enviado, canal, codigo?, destinoMascarado, situacao }` (`nao-escalado` / `ja-cadastrado` / `sem-telefone`) |
| POST | `/rastreamento/dispositivos/primeiro-acesso/confirmar` | `{ cpf, codigo }` → `{ ticket, motoristaId, nome, termo: { versao, texto } }` |
| POST | `/rastreamento/dispositivos/primeiro-acesso/rosto` | `{ ticket, fotoBase64, termoVersao, latitude?, longitude? }` → 201 `{ fotoHash }` |
| POST | `/rastreamento/dispositivos/verificacoes-faciais` | conferência (idempotente pelo `idLocal`) → 201 |
| POST | `/rastreamento/dispositivos/liberacao` | `{ idLocal, codigo }` → `{ motoristaId, nome }` |

Painel (JWT): `motoristas/{id}/biometria` (GET situação, GET `/foto`, DELETE apaga),
`veiculos/{id}/liberacao-motorista` (POST, Rastreamento/Edição), `rastreamento/verificacoes-faciais`
(GET, + `/{id}/selfie`).

**Teste no tablet com servidor local:** os builds debug/profile aceitam HTTP só para `localhost`.
`adb reverse tcp:5080 tcp:5080` + `flutter build apk --profile
--dart-define=SMSMAIS_API_BASE_URL=http://localhost:5080` + `Tfd__Otp__ModoTeste=true` no servidor
(o código do primeiro acesso aparece na tela). O cabo precisa ficar ligado: sem ele o túnel cai.
