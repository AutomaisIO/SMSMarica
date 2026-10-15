using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Biometria;

// Login do motorista no tablet pelo rosto (ADR-0073). O tablet fala pelo token do veículo; o painel,
// pelo JWT com as permissões dos módulos Rastreamento e Motoristas.

// ---------------------------------------------------------------- tablet

/// <summary>Motorista escalado hoje no veículo do tablet. <paramref name="FotoHash"/> nulo = ainda
/// não cadastrou o rosto (vai pelo primeiro acesso).</summary>
public sealed record MotoristaEscaladoDto(
    Guid MotoristaId,
    string Nome,
    Guid RotaId,
    bool TemReferencia,
    string? FotoHash,
    DateTime? ReferenciaCadastradaEm);

public sealed record ReferenciaFacialDto(Guid MotoristaId, string FotoBase64, string FotoHash, DateTime CadastradaEm);

public sealed record SolicitarCodigoMotoristaRequest(string Cpf);

/// <summary>Resposta do pedido de código do primeiro acesso. <paramref name="Situacao"/> diz por
/// que nada saiu (ver <see cref="SituacaoPrimeiroAcesso"/>); <paramref name="Codigo"/> só vem no
/// modo de teste.</summary>
public sealed record CodigoMotoristaEmitidoDto(
    bool Enviado,
    string Canal,
    string? Codigo,
    int ValidadeSegundos,
    string? DestinoMascarado,
    string? Situacao);

public static class SituacaoPrimeiroAcesso
{
    /// <summary>O CPF não é de um motorista escalado hoje neste veículo.</summary>
    public const string NaoEscalado = "nao-escalado";

    /// <summary>O rosto já está cadastrado; para refazer, a central apaga no painel.</summary>
    public const string JaCadastrado = "ja-cadastrado";

    /// <summary>O cadastro do motorista não tem celular para receber o código.</summary>
    public const string SemTelefone = "sem-telefone";
}

public sealed record ConfirmarCodigoMotoristaRequest(string Cpf, string Codigo);

public sealed record TermoBiometriaDto(string Versao, string Texto);

/// <summary>Código conferido: o <paramref name="Ticket"/> autoriza mandar a foto em seguida.</summary>
public sealed record PrimeiroAcessoLiberadoDto(string Ticket, Guid MotoristaId, string Nome, TermoBiometriaDto Termo);

public sealed record CadastrarRostoRequest(
    string Ticket,
    string FotoBase64,
    string TermoVersao,
    double? Latitude,
    double? Longitude);

public sealed record ReferenciaCadastradaDto(Guid MotoristaId, string FotoHash, DateTime CadastradaEm);

public sealed record RegistrarVerificacaoFacialRequest(
    Guid IdLocal,
    Guid? MotoristaId,
    MomentoVerificacaoFacial Momento,
    bool Aprovada,
    double? Nota,
    double? Limiar,
    bool ProvaDeVida,
    string? ModeloVersao,
    double? Latitude,
    double? Longitude,
    double? PrecisaoM,
    Guid? RotaId,
    DateTime CapturadaEm,
    string? SelfieBase64);

public sealed record VerificacaoRegistradaDto(Guid Id, DateTime RecebidaEm);

public sealed record UsarLiberacaoRequest(Guid IdLocal, string Codigo, double? Latitude, double? Longitude);

public sealed record MotoristaLiberadoDto(Guid MotoristaId, string Nome);

// ---------------------------------------------------------------- painel

public sealed record BiometriaMotoristaDto(
    bool Cadastrada,
    DateTime? CadastradaEm,
    string? VeiculoPlaca,
    string? TermoVersao);

public sealed record FotoBiometriaDto(string FotoBase64);

public sealed record GerarLiberacaoRequest(Guid MotoristaId);

public sealed record LiberacaoGeradaDto(string Codigo, DateTime ExpiraEm, string MotoristaNome, string VeiculoPlaca);

public sealed record FiltroVerificacoesFaciais(Guid? VeiculoId, Guid? MotoristaId, DateOnly? Dia);

public sealed record VerificacaoFacialDto(
    Guid Id,
    Guid VeiculoId,
    string VeiculoPlaca,
    Guid? MotoristaId,
    string? MotoristaNome,
    MomentoVerificacaoFacial Momento,
    MeioVerificacaoMotorista Meio,
    bool Aprovada,
    double? Nota,
    double? Limiar,
    bool ProvaDeVida,
    double? Latitude,
    double? Longitude,
    DateTime CapturadaEm,
    DateTime RecebidaEm,
    bool TemSelfie,
    string? LiberadaPorNome);
