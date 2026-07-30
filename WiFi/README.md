# WiFi — Enlace de rádio entre as unidades especializadas

Projeto de enlace de rádio (PtP/PtMP) interligando as unidades especializadas da SMS Maricá.

## Unidades e coordenadas

A relação de unidades em [`unidades.csv`](./unidades.csv) foi copiada de `Telefonia/registro/unidades.csv` (fonte canônica de id/nome/grupo/endereço/gestor) e enriquecida com **lat/long consultadas no Google Maps em 29/07/2026** a partir do endereço de cada unidade.

Colunas de rede/VPN/MikroTik ficaram de fora de propósito — para isso, usar o CSV da Telefonia.

### Coluna `precisao`

| Valor | Significado |
|---|---|
| `EXATA` | Google resolveu rua **e número** do endereço. |
| `POI` | Ponto do próprio estabelecimento no Google Maps (HMCML, UPAs, CEREST). |
| `RUA` | Endereço sem número utilizável (s/n ou Qd/Lt) — coordenada é o eixo da rua. Confirmar o ponto exato em campo. |
| `BAIRRO` | Google não achou a rua — coordenada é o centroide do bairro. **Não usar para cálculo de enlace.** |
| `VERIFICAR` | O Google devolveu um logradouro diferente do cadastrado — conferir antes de usar. |
| `SEM_PONTO_FIXO` / `SEM_ENDERECO` | Sem coordenada. |

### Pendências de precisão

- **id 5 — CAPSI**: endereço cadastrado é "Rua Homero de Queiroz da Silva, 363", mas o Google resolveu "R. Eugênia Modesto da Silva, 363 - Eldorado" (possível rua renomeada). Confirmar no local.
- **id 15 — Núcleo de Imunização**: "Rua Um, 50 - São José do Imbassaí" não existe no Google; coordenada é o centroide do bairro. Levantar o ponto em campo (GPS do celular do gestor André resolve).
- **id 17 — SAE**: "Rua Joaquim Ferreira da Silva" resolveu como "R. Noventa e Três" (Araçatiba/Eldorado). Confirmar.
- **id 24 — Unidade Móvel Clube Maricá**: a rua correta no Google é "R. **Álvares** de Castro, 172" (o cadastro diz "Álvaro"); coordenada obtida com o nome correto.
- **id 29 — Posto Santa Rita**: sem endereço na relação da Telefonia.
- **ids 0, 8, 19, 20** (`RUA`): coordenada no eixo da rua, sem número — bom o suficiente para estudo de viabilidade, refinar antes do projeto executivo.

## Estudo de visada (SRTM 30 m — 29/07/2026)

Perfis de elevação de 50 enlaces candidatos em [`perfis-visada.json`](./perfis-visada.json) (dataset SRTM 30 m via opentopodata.org). Premissas: 5,8 GHz, curvatura k=4/3, folga = LOS − (terreno + curvatura + 60% da 1ª zona de Fresnel), mastros de **15 m no hub, 10 m nas pontas, 20 m nas repetidoras**. Mapa interativo com topologia e perfis publicado como artifact ("Enlace de Rádio — Unidades Especializadas SMS Maricá").

### Topologia proposta

| Enlace | Dist. | Folga | Papel |
|---|---|---|---|
| Sede SMS (hub) → **REP-1** Serra do Espraiado (~345 m, `-22.9162,-42.9030`) | 9,14 km | **+18,2 m** | backbone oeste |
| **REP-1** → **REP-2** Morro Caju/Itaocaia (~301 m, `-22.90939,-42.78718`) | 11,90 km | **+25,2 m** | backbone leste (pico a pico) |
| REP-1 → UPA Inoã | 2,82 km | +13,8 m | |
| REP-1 → CEO Itaipuaçu | 7,58 km | +11,4 m | |
| REP-1 → Núcleo de Imunização | 3,70 km | +14,3 m | ponta com coordenada de bairro |
| REP-2 → CRAD | 1,44 km | +5,8 m | |
| REP-2 → TFD/Transporte | 3,07 km | +3,6 m | |
| REP-2 → UPA Ponta Negra | 6,19 km | **+2,2 m** | apertada — confirmar em campo |
| Hub → Complexo Regulador | 3,57 km | +9,6 m | PtP direto |

### Grupos de visada

- **Grupo A — direto do hub (PtMP):** 15 pontas com visada limpa (ids 0, 1, 3, 4, 5, 6, 10, 11, 16, 17, 18, 19, 24, 26 + prédios compartilhados) + 2 marginais que fecham com mastro mais alto: TACO 13/25 (−0,8 m) e SRT III 20 (−4 m).
- **Grupo B — via REP-1 (serra):** CEO Itaipuaçu (8), UPA Inoã (27), Núcleo de Imunização (15). A serra bloqueia totalmente o caminho direto SMS→Inoã (obstrução de 345 m, folga −321 m).
- **Grupo C — via REP-2 (Caju):** CRAD (9), TFD/Transporte (21/22), UPA Ponta Negra (28). Nenhum dos três tem visada direta do hub (morros de 66–135 m).
- **Grupo D — pendente de solução local:** CAF (2, −10 m) e CEO Boqueirão (7, −20,6 m) — morros de 36–44 m colados no caminho; opções: mastro alto, NLOS curto ou fibra.
- Testados e descartados: CRAD como sub-hub do norte (morro de 66 m colado nele bloqueia tudo) e morro de 135 m a leste do centro (é encosta, não cume).

**Limites:** SRTM é modelo de superfície de ~2000 (não vê prédios/vegetação atuais) — folgas < 10 m exigem confirmação em campo; as coordenadas das repetidoras são picos aproximados do raster, não sites reais (verificar acesso/energia/propriedade).

### Observações gerais

- Mesmos prédios (um rádio atende os dois): 12+14 (sede SMS), 13+25 (prédio TACO), 21+22 (Gaivotas 12).

## Equipamentos — opções Premium e Intermediária (linha Ubiquiti)

Especificação sobre a topologia calculada acima. Gestão unificada no UISP nas duas opções. **Famílias Wave (60 GHz) e LTU (5 GHz) não interoperam — não misturar dentro do mesmo segmento.**

### Opção PREMIUM — Full Wave (60 GHz com failover 5 GHz integrado)

Multi-gigabit em tempo bom; na chuva forte os enlaces longos **degradam (não caem)** para o rádio backup 5 GHz embutido em cada equipamento.

| Segmento | Equipamento | Qtde | Banda |
|---|---|---|---|
| Backbones hub→REP-1 (9,1 km) e REP-1→REP-2 (11,9 km) | [Wave Pro](https://techspecs.ui.com/uisp/60ghz-wireless/wave-pro) (par) | 4 | 5,4 Gbps (chuva: ~300–800 Mbps no backup) |
| Hub: 2 setores de 90° cobrindo o arco de ~170° | [Wave AP Micro](https://techspecs.ui.com/uisp/60ghz-wireless/wave-a-p-micro) | 2 | 5 Gbps/setor |
| Pontas do miolo (17) + Complexo Regulador (3,6 km ≤ 4 km) | [Wave Nano](https://techspecs.ui.com/uisp/60ghz-wireless/wave-nano) | 18 | 2 Gbps/ponta |
| REP-2: setor oeste (CRAD 1,4 km + TFD 3,1 km, mesmo azimute 274°) | Wave AP Micro + 2× Wave Nano | 1+2 | 2 Gbps/ponta |
| Saltos PtP: REP-1→Inoã (2,8), REP-1→Imunização (3,7), REP-1→Itaipuaçu (7,6), REP-2→Ponta Negra (6,2) | [Wave LR](https://store.ui.com/us/en/products/wave-lr) (pares PtP) | 8 | 2 Gbps/salto |

4 modelos · ~US$ 15 mil em tabela US (importado no Brasil ≈ 2–2,5×). Os azimutes na REP-1 são espalhados demais (121°–276°) para um setor de 90° — por isso saltos PtP com Wave LR em vez de AP Micro.

### Opção INTERMEDIÁRIA — 5 GHz TDMA (imune a chuva, 3 famílias)

Banda constante em qualquer clima; teto de ~600 Mbps por ponta e ~1 Gbps no tronco.

| Segmento | Equipamento | Qtde | Banda |
|---|---|---|---|
| Backbones (9,1 e 11,9 km) | [airFiber 5XHD](https://dl.ubnt.com/datasheets/airfiber/airFiber_5XHD_DS.pdf) (par) | 4 | ~1 Gbps constante |
| Setoriais no hub (2), REP-1 (2) e REP-2 (2) | LTU Rocket + setor airMAX 60° | 6 | TDMA, 31+ clientes |
| Todas as pontas (miolo + reps, 0,12–7,6 km) | LTU LR | ~24 | 300–600 Mbps/ponta |

3 modelos · ~US$ 9,6 mil em tabela US. (Opção econômica em MikroTik — NetMetal ax + mANTBox 19s + LHG 5 ac, ~400–700 Mbps no tronco — fica registrada como alternativa de menor custo.)

### Pontos principais — o que é melhor

- **Backbone (ponto mais crítico da rede)**: banda máxima = **Wave Pro** (5,4 Gbps, degrada na chuva); disponibilidade máxima = **airFiber 5XHD** (1 Gbps sempre). Quem não tolera degradação nenhuma no tronco usa AF-5XHD mesmo na opção Premium (híbrido).
- **Hub (célula do centro)**: **Wave AP Micro ×2** é superior em qualquer cenário — miolo ≤ 2,3 km é o caso perfeito de 60 GHz (gigabit por ponta, espectro vazio, failover embutido, chuva quase não afeta nessa distância).
- **Repetidoras**: a eletrônica é secundária ao site — energia, torre e acesso na Serra/Caju definem o projeto. Em ambas as opções cada REP leva 4 rádios + switch PoE.
