using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;

/// <summary>Resposta ao disparo do lote: aceito (202) e enfileirado.</summary>
public sealed record MapeamentoLoteAceitoDto(int UnidadesTotal, string Mensagem);

/// <summary>
/// Snapshot do lote em curso (ou <c>null</c> quando não há nenhum). É memória, não banco: só o
/// processo sabe se ainda está vivo. O que sobra depois da execução está em
/// <see cref="MapeamentoLoteExecucaoDto"/>.
/// </summary>
public sealed record MapeamentoLoteStatusDto(
    bool EmExecucao,
    DisparoSincronizacao Disparo,
    /// <summary>Fase atual, para a tela não mostrar "0/43" enquanto ainda está descobrindo.</summary>
    string Fase,
    int UnidadesTotal,
    int UnidadesFeitas,
    string? UnidadeAtual,
    int UnidadesNoSisreg,
    int UnidadesCriadas,
    int UnidadesMapeadas,
    int UnidadesPuladas,
    int RequisicoesFeitas,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int UnidadesComErro,
    /// <summary>Requisições ainda disponíveis na janela de 60 min antes do teto anti-robô.</summary>
    int OrcamentoRestante,
    DateTime IniciadoEm,
    string? UltimoErro);

/// <summary>Configuração do disparo automático do lote de mapeamento.</summary>
public sealed record MapeamentoLoteAgendamentoDto(
    bool Ativo,
    string HoraLocal,
    /// <summary>
    /// Modo de CARGA INICIAL: em vez de uma rodada por dia, dispara uma atrás da outra assim que o
    /// orçamento da hora permite, até nenhuma unidade estar sem primeiro mapeamento — aí se desliga
    /// sozinho. Existe porque a carga inicial da rede é da ordem de 1.500 requisições e o teto é
    /// por hora: sem isto, seriam semanas de uma rodada por dia.
    /// </summary>
    bool Bootstrap = false,
    /// <summary>Unidades que ainda nunca foram mapeadas — o que falta para a carga inicial acabar.</summary>
    int PendentesPrimeiroMapeamento = 0,
    /// <summary>Requisições ainda disponíveis na janela de 60 min.</summary>
    int OrcamentoRestante = 0);

/// <summary>Ligar/desligar o disparo automático e a que horas (Brasília, HH:mm).</summary>
public sealed record SalvarMapeamentoLoteAgendamentoRequest(
    bool Ativo,
    string HoraLocal,
    /// <summary>Omitido mantém o modo de carga inicial como está.</summary>
    bool? Bootstrap = null);

/// <summary>Uma execução do "sincroniza tudo", para a lista de sincronizações recentes.</summary>
public sealed record MapeamentoLoteExecucaoDto(
    Guid Id,
    DisparoSincronizacao Disparo,
    StatusVarredura Status,
    int UnidadesNoSisreg,
    int UnidadesCriadas,
    int UnidadesComCnesPreenchido,
    int UnidadesTotal,
    int UnidadesMapeadas,
    int UnidadesPuladas,
    int UnidadesComErro,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int Requisicoes,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos);

/// <summary>Detalhe por unidade de uma execução — o "quantos médicos por unidade" da tela.</summary>
public sealed record MapeamentoLoteExecucaoItemDto(
    Guid Id,
    Guid UnidadeId,
    string UnidadeNome,
    string? Cnes,
    bool UnidadeCriada,
    ResultadoUnidadeLote Resultado,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProfissionaisAusentes,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int Requisicoes,
    string? Observacao);

/// <summary>
/// Preparar a rede inteira para o sincronismo diário, de uma vez: habilita todos os profissionais
/// e procedimentos já mapeados e liga a varredura diária de cada unidade em horários escalonados.
/// </summary>
/// <param name="IntervaloMinutos">Espaço entre os horários de duas unidades.</param>
/// <param name="HoraInicialLocal">
/// Onde a distribuição começa (Brasília, HH:mm). 18:00: bem depois do fim do bloqueio (15:00) e já
/// fora do expediente, então a varredura não disputa a sessão do SISREG com quem está atendendo.
/// </param>
/// <param name="DiasAFrente">Janela de agenda que cada unidade importa por dia.</param>
/// <param name="Habilitar">Ligar todos os médicos e procedimentos mapeados de cada unidade.</param>
public sealed record PrepararRedeRequest(
    // O atraso de uma unidade não faz a seguinte perder a vez: com varredura viva o scheduler não
    // dispara e NÃO mexe no ProximoRunEm, então a agenda vencida entra assim que a saída libera
    // (DecididorVarreduraSisreg). É o que permite espaçar por tempo sem medo — medido: mediana de
    // 43s por varredura, com um pico de 54 min.
    int IntervaloMinutos = 10,
    string HoraInicialLocal = "18:00",
    int DiasAFrente = 21,
    bool Habilitar = true);

/// <summary>O que a preparação deixou pronto.</summary>
public sealed record PrepararRedeDto(
    int UnidadesPreparadas,
    int ProfissionaisHabilitados,
    int ProcedimentosHabilitados,
    string PrimeiroHorario,
    string UltimoHorario,
    IReadOnlyList<PrepararRedeUnidadeDto> Unidades,
    string Mensagem);

/// <summary>Horário que coube a cada unidade.</summary>
public sealed record PrepararRedeUnidadeDto(
    Guid UnidadeId, string Nome, string HoraLocal, int Profissionais, int Procedimentos);

/// <summary>
/// Prévia da distribuição, sem gravar nada. Existe para o operador ver a que horas a fila termina
/// <b>antes</b> de confirmar: com 45 unidades, a diferença entre 10 e 20 minutos é terminar 01:20
/// ou empurrar as últimas para a tarde do dia seguinte — e isso não se descobre olhando os dois
/// campos.
/// </summary>
public sealed record PreverAgendamentoRequest(int IntervaloMinutos, string HoraInicialLocal);

/// <summary>O que a distribuição vai produzir.</summary>
public sealed record PreverAgendamentoDto(
    int Unidades,
    string PrimeiroHorario,
    string UltimoHorario,
    /// <summary>Quantas não cabem na madrugada e caem depois do bloqueio, na tarde seguinte.</summary>
    int ForaDaMadrugada,
    /// <summary>Frase pronta para a tela — inclui o alerta quando há unidade fora da madrugada.</summary>
    string Resumo);

/// <summary>Ligar ou desligar a importação diária de TODAS as unidades de uma vez.</summary>
public sealed record AlternarAgendamentoRedeRequest(bool Ativo);

/// <summary>Quantas agendas mudaram de estado.</summary>
public sealed record AlternarAgendamentoRedeDto(int UnidadesAfetadas, int UnidadesAtivas, string Mensagem);
