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

- Primeira tela depois do login (rota `/deslocamento`); Rota do dia continua acessível.
- Mapa Google (`GoogleMapsMapView` do `google_navigation_flutter`, sem sessão de navegação — não
  cobra como navegação) com "minha localização" e câmera seguindo.
- Botão grande **Iniciar / Parar**:
  - Iniciar zera odômetro e cronômetro e começa a somar;
  - Parar congela velocidade, odômetro e tempo na tela;
  - Iniciar de novo recomeça do zero.
- Velocidade instantânea (km/h) e odômetro (km com 2 casas, metros abaixo de 1 km) e tempo do trajeto.
- Filtro de GPS: ignora fix com precisão > 30 m, saltos que implicariam > 180 km/h e passos menores que
  a precisão quando parado (evita o odômetro "andar" com o carro parado).
- Envio ao servidor (se vinculado): a cada 5 s com deslocamento ativo, a cada 60 s parado; fila em
  memória com até 500 pontos se a rede cair.
