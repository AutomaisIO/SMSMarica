using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Sisreg.Base.Dtos;

/// <summary>
/// Onde o atendimento está, do ponto de vista de quem acompanha a fila — derivado do que já temos
/// na base (status da solicitação, chegada relida do SISREG e lista oficial de faltas). Mesma régua
/// da ficha do paciente (<c>AgendamentosPacienteService</c>): a unidade executante é quem aponta
/// Confirmado ou Falta, e o que passou do dia sem nenhum dos dois é <see cref="Pendente"/>.
/// </summary>
public enum SituacaoAgendamentoSisreg
{
    /// <summary>Solicitada, ainda sem data (na fila do SISREG).</summary>
    NaFila,

    /// <summary>Com data de hoje em diante.</summary>
    Agendada,

    /// <summary>A data passou e a unidade não apontou nem chegada nem falta no SISREG.</summary>
    Pendente,

    /// <summary>Chegada confirmada no SISREG, registrada na recepção, ou solicitação realizada.</summary>
    Compareceu,

    /// <summary>Está na lista oficial de faltas do SISREG (ou a tela de agenda marcou falta).</summary>
    Faltou,

    /// <summary>Cancelada.</summary>
    Cancelada,
}

/// <summary>Qual data o período recorta.</summary>
public enum EixoDataConsultaSisreg
{
    /// <summary>Data do agendamento (dia de Brasília). Quem está "na fila" não tem — não aparece.</summary>
    Agendamento,

    /// <summary>Data em que foi solicitada no SISREG.</summary>
    Solicitacao,
}

/// <summary>
/// Filtro da consulta à nossa base. Lista vazia = sem filtro naquele campo (todas as unidades
/// do escopo, todas as situações, todos os procedimentos).
/// </summary>
public sealed record ConsultaBaseSisregFiltro(
    DateOnly Inicio,
    DateOnly Fim,
    EixoDataConsultaSisreg Eixo = EixoDataConsultaSisreg.Agendamento,
    IReadOnlyList<Guid>? UnidadeIds = null,
    IReadOnlyList<SituacaoAgendamentoSisreg>? Situacoes = null,
    IReadOnlyList<string>? Procedimentos = null,
    bool IncluirExames = true,
    bool IncluirConsultas = true,
    int Pagina = 1,
    int Tamanho = 100);

public sealed record ContagemSituacaoDto(SituacaoAgendamentoSisreg Situacao, int Quantidade);

/// <summary>Um atendimento (uma solicitação) no resultado.</summary>
/// <param name="DetalheId">
/// Id que abre o detalhe (<c>GET /solicitacoes/{id}</c>): o do exame de imagem quando há, senão o da
/// própria solicitação — o mesmo critério da ficha do paciente.
/// </param>
public sealed record AgendamentoBaseSisregDto(
    Guid SolicitacaoId,
    Guid DetalheId,
    Guid PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    string? CodigoSolicitacao,
    string Procedimento,
    CategoriaSolicitacao Categoria,
    DateTime? DataAgendada,
    DateOnly? DataSolicitacao,
    string? UnidadeExecutante,
    SituacaoAgendamentoSisreg Situacao);

/// <param name="TotalPessoas">Pacientes distintos no resultado inteiro (não só na página).</param>
public sealed record ConsultaBaseSisregResultado(
    int TotalAtendimentos,
    int TotalPessoas,
    IReadOnlyList<ContagemSituacaoDto> PorSituacao,
    int Pagina,
    int Tamanho,
    IReadOnlyList<AgendamentoBaseSisregDto> Itens);

public sealed record OpcaoUnidadeConsultaDto(Guid Id, string Nome, int Quantidade);

public sealed record OpcaoProcedimentoConsultaDto(string Nome, int Quantidade);

/// <summary>Unidades executantes e procedimentos que têm atendimento no período (dentro do escopo).</summary>
public sealed record OpcoesConsultaBaseSisregDto(
    IReadOnlyList<OpcaoUnidadeConsultaDto> Unidades,
    IReadOnlyList<OpcaoProcedimentoConsultaDto> Procedimentos);
