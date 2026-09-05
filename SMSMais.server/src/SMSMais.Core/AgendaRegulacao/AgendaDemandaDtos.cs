namespace SMSMais.Core.AgendaRegulacao;

/// <summary>
/// Recorte da análise de <b>demanda</b>. Diferente do <see cref="AgendaFiltro"/>, que parte da
/// escala: aqui se olha a solicitação, então o filtro é por quem pediu, o que pediu e para onde foi.
///
/// <para><b><see cref="EixoData"/> muda a pergunta, não só a ordenação.</b> Com
/// <c>agendada</c> a tela responde "o que está marcado neste período"; com <c>solicitada</c>,
/// "o que foi pedido neste período". A segunda é a leitura certa para saber se a fila está
/// crescendo — a primeira esconde o pedido que ainda não conseguiu vaga aqui dentro.</para>
/// </summary>
public sealed record DemandaFiltro(
    DateOnly De,
    DateOnly Ate,
    string EixoData = "agendada",
    Guid? UnidadeExecutanteId = null,
    Guid? UnidadeSolicitanteId = null,
    string? Procedimento = null,
    int? Prioridade = null);

/// <summary>
/// Os números do topo da tela de demanda. A espera é medida em <b>dias corridos</b> entre a data
/// da solicitação e o dia agendado.
/// </summary>
/// <param name="Regulados">Solicitações com data agendada no recorte.</param>
/// <param name="ComEspera">Quantas têm as duas datas e portanto entram no cálculo de espera.</param>
/// <param name="EsperaMediana">
/// Mediana, não média: a cauda desta distribuição vai a anos (máximo medido: 1.859 dias) e puxaria
/// a média para um número que não descreve o caso típico de ninguém.
/// </param>
/// <param name="EsperaP90">Nove em cada dez esperaram até aqui. É o número da pior experiência real.</param>
/// <param name="EsperaRegulacaoMediana">
/// Da regulação até o dia agendado. A diferença para <paramref name="EsperaMediana"/> é o tempo que
/// o pedido passou <b>antes</b> de ser regulado — medido em 05/09/2026, 57 contra 29 dias: metade da
/// espera acontece antes da fila que a regulação enxerga.
/// </param>
/// <param name="Inconsistentes">
/// Agendado para <b>antes</b> de ter sido solicitado. É data errada na origem, não fila negativa;
/// fica exposto em vez de descartado em silêncio para que o número possa ser corrigido.
/// </param>
public sealed record DemandaResumoDto(
    int Regulados,
    int ComEspera,
    int EsperaMediana,
    int EsperaP90,
    int EsperaMaxima,
    int EsperaRegulacaoMediana,
    int Ate30Dias,
    int Acima180Dias,
    int Inconsistentes,
    int Procedimentos,
    int UnidadesSolicitantes,
    int Confirmados);

/// <summary>
/// Um procedimento na fila. <paramref name="Volume"/> e <paramref name="EsperaMediana"/> são eixos
/// <b>independentes</b> — medido em 05/09/2026, Mamografia bilateral tinha 2.301 pedidos com 56 dias
/// de espera e Ultrassonografia de mamas 655 com 260. Ordenar só por volume esconde o gargalo.
/// </summary>
public sealed record DemandaProcedimentoDto(
    string Procedimento,
    int Volume,
    int EsperaMediana,
    int EsperaP90,
    int Acima90Dias,
    int UnidadesSolicitantes,
    int UnidadesExecutantes);

/// <summary>Histograma da espera. Faixas fixas para que duas consultas sejam comparáveis.</summary>
public sealed record DemandaFaixaEsperaDto(int Ordem, string Rotulo, int Volume);

/// <summary>De onde vem o pedido (unidade solicitante) ou para onde vai (executante).</summary>
public sealed record DemandaOrigemDto(string Chave, string Rotulo, int Volume, int EsperaMediana);

/// <summary>Volume e espera mês a mês — é o que diz se a fila está crescendo ou cedendo.</summary>
public sealed record DemandaSerieDto(DateOnly Mes, int Volume, int EsperaMediana, int EsperaP90);

/// <summary>Opções dos filtros da demanda, montadas do que existe de fato em solicitação.</summary>
public sealed record DemandaOpcoesDto(
    IReadOnlyList<OpcaoDto> UnidadesExecutantes,
    IReadOnlyList<OpcaoDto> UnidadesSolicitantes,
    IReadOnlyList<OpcaoDto> Procedimentos);

/// <summary>
/// Oferta e ocupação dia a dia — a série que a tela de análise plota. Existe separada do resumo
/// porque responde a outra pergunta: não "quanto no total", mas "quando".
/// </summary>
public sealed record AgendaSerieDiaDto(DateOnly Data, int Vagas, int Agendados);

/// <summary>
/// Até onde o dado <b>existe</b>. Sem isto toda tela deste módulo mente por omissão: medido em
/// 05/09/2026, <c>data_agendada</c> só ia de 28/05 a 09/10/2026 — a janela que a varredura tinha
/// importado —, e setembro sozinho concentrava 79% dos registros por ser o mês varrido, não por
/// ser o mês movimentado.
///
/// <para>A consequência é estatística e não se resolve com cálculo: só entram na base as
/// solicitações cujo agendamento caiu nessa faixa. Coorte antiga só aparece se demorou muito
/// (viés para cima); coorte recente, só se foi rápida (viés para baixo). Por isso a leitura
/// segura é por <b>data agendada dentro da faixa coberta</b> — "quem foi atendido nestes dias
/// esperou X" é afirmação exata; "a fila cresceu" não é.</para>
/// </summary>
/// <param name="UltimoDiaDeEscala">
/// Até quando o SISREG publicou escala. A distância entre este dia e
/// <paramref name="UltimoDiaAgendado"/> é o futuro que ainda falta varrer.
/// </param>
public sealed record AgendaCoberturaDto(
    DateOnly? PrimeiroDiaAgendado,
    DateOnly? UltimoDiaAgendado,
    DateOnly? UltimoDiaDeEscala,
    int Agendamentos);
