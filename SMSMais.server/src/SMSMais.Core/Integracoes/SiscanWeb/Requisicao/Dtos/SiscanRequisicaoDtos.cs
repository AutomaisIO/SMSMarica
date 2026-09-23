namespace SMSMais.Core.Integracoes.SiscanWeb.Requisicao.Dtos;

/// <summary>Um profissional do combo do SISCAN. O CNS é a chave — o índice é posicional.</summary>
/// <param name="Indice">
/// Valor do <c>&lt;option&gt;</c>. <b>Não guardar:</b> muda entre diagnóstica e rastreamento (o
/// mesmo profissional foi 8 numa lista e 9 na outra, medido em 22/09/2026).
/// </param>
public sealed record SiscanResponsavelDto(string Indice, string Nome, string Cns);

/// <summary>Uma resposta que vai para o SISCAN, com o rótulo para a tela conferir antes de mandar.</summary>
public sealed record SiscanCampoEnvioDto(string Pergunta, string Resposta);

/// <summary>
/// O que a tela precisa para montar o modal de "Gerar Requisição SISCAN": quem pode assinar, o
/// que será enviado e o que ainda falta responder.
/// </summary>
public sealed record SiscanPreparoDto(
    bool JaGerada,
    string? Protocolo,
    string? NumeroExame,
    string PacienteNome,
    string CnesUnidade,
    string UnidadeNome,
    string TipoMamografia,
    string TipoMamografiaRotulo,
    IReadOnlyList<SiscanResponsavelDto> Responsaveis,
    string? CnsResponsavelSugerido,
    string? NomeSolicitanteDaFicha,
    IReadOnlyList<SiscanCampoEnvioDto> Envio,
    IReadOnlyList<LacunaAnamnese> Lacunas);

/// <summary>Quem assina. Vem por CNS porque o índice do combo não é estável.</summary>
public sealed record SiscanGerarRequest(string CnsResponsavel);

/// <summary>O que ficou carimbado no nosso exame depois de gerar.</summary>
public sealed record SiscanRequisicaoDto(
    string Protocolo, string NumeroExame, DateTime GeradaEm, string ResponsavelNome);
