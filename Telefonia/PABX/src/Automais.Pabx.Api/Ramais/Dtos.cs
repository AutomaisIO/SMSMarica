using Automais.Pabx.Api.Data.Entities;

namespace Automais.Pabx.Api.Ramais;

public sealed record RamalDto(
    string Numero,
    int UnidadeId,
    string? UnidadeNome,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId,
    bool Ativo,
    OrigemRamal Origem,
    TipoRamal Tipo,
    string Contexto,
    IReadOnlyList<string> Codecs,
    int CallLimit,
    string? DonoSistema,
    string? DonoId,
    DateTime CriadoEm,
    DateTime AtualizadoEm);

/// <summary>Retornado só na criação/reset: única vez em que o secret sai em claro.</summary>
public sealed record RamalComSecretDto(RamalDto Ramal, string Secret);

public sealed record CriarRamalRequest(
    string Numero,
    int UnidadeId,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId,
    TipoRamal Tipo = TipoRamal.Fisico,
    string? DonoSistema = null,
    string? DonoId = null);

public sealed record AtualizarRamalRequest(
    int UnidadeId,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId,
    bool Ativo);

public sealed record AdotarRamaisRequest(IReadOnlyList<string> Numeros, int UnidadeId);

public sealed record AdocaoResultadoDto(
    IReadOnlyList<string> Adotados,
    IReadOnlyList<string> JaInventariados,
    IReadOnlyList<string> NaoEncontrados);

public sealed record StatusRamalDto(
    string Numero,
    string? Descricao,
    int UnidadeId,
    string? UnidadeNome,
    int? UnidadeDetectadaId,
    OrigemRamal Origem,
    bool Online,
    string? Ip,
    int? LatenciaMs,
    bool EmChamada,
    string? StatusBruto);

public sealed record StatusGeralDto(
    bool AmiDisponivel,
    DateTime ConsultadoEm,
    IReadOnlyList<StatusRamalDto> Ramais);

/// <summary>Configurações de SIP editáveis por ramal (o que hoje era fixo no gerador).</summary>
public sealed record ConfigRamalDto(string Contexto, IReadOnlyList<string> Codecs, int CallLimit);

public sealed record AtualizarConfigRamalRequest(string Contexto, IReadOnlyList<string> Codecs, int CallLimit);

/// <summary>Vincula o ramal a um dono em sistema externo; ambos nulos desvinculam.</summary>
public sealed record DefinirDonoRequest(string? Sistema, string? Id);

public sealed record FaixaLivreDto(TipoRamal Tipo, int? Inicio, int? Fim, IReadOnlyList<string> Sugestoes);

public sealed record IceServerDto(string Urls, string? Username, string? Credential);

/// <summary>
/// Tudo que o softphone do navegador precisa para registrar. Contém a senha SIP em claro:
/// só sai para quem tem a chave de serviço e nunca é logado nem cacheado.
/// </summary>
public sealed record CredencialSipDto(
    string Ramal,
    string UsuarioSip,
    string Senha,
    string Dominio,
    string Uri,
    string WssUrl,
    string? NomeExibicao,
    IReadOnlyList<IceServerDto> IceServers);

/// <summary>Detalhe do peer no chan_sip (AMI SIPshowpeer), para diagnóstico de um ramal.</summary>
public sealed record PeerDetalheDto(string Numero, bool Registrado, IReadOnlyDictionary<string, string> Campos);
