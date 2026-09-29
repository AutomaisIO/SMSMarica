using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.UnidadesAtendimento.Dtos;

public sealed record UnidadeAtendimentoDto(
    Guid Id,
    string Nome,
    EnderecoDto? Endereco,
    string? Telefone,
    string? Observacoes,
    double? Latitude,
    double? Longitude,
    bool Externa,
    bool Ativo,
    int TratamentosAtivos,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

public sealed record UnidadeAtendimentoListItemDto(
    Guid Id,
    string Nome,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    bool TemCoordenada,
    bool Externa,
    bool Ativo,
    int TratamentosAtivos);

/// <summary>Opção do seletor de destino no cadastro de tratamento (só as ativas).</summary>
public sealed record UnidadeAtendimentoOpcaoDto(
    Guid Id,
    string Nome,
    string? Bairro,
    string? Cidade,
    string? Uf,
    bool TemCoordenada);

/// <summary>
/// Cadastro/edição. <see cref="Latitude"/>/<see cref="Longitude"/> vêm do pin no mapa; sem eles o
/// serviço tenta geocodificar o endereço — e recusa se não achar, porque destino sem coordenada
/// não entra na rota.
/// </summary>
public sealed record SalvarUnidadeAtendimentoRequest(
    string Nome,
    EnderecoDto? Endereco,
    string? Telefone,
    string? Observacoes,
    double? Latitude,
    double? Longitude,
    /// <summary>Destino fora do município (TFD). Marcado por quem cadastra — derivar da cidade
    /// dependeria do endereço da instituição, que nem toda instância preenche. Padrão = fora,
    /// porque é o caso típico do transporte.</summary>
    bool Externa = true);
