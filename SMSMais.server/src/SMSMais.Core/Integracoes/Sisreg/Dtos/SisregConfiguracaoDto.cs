using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Sisreg.Dtos;

/// <summary>
/// Configuração SISREG exposta na tela. Nunca devolve senha/token: apenas sinaliza
/// se já estão definidos (<see cref="SenhaDefinida"/> / <see cref="TokenDefinido"/>).
/// </summary>
public sealed record SisregConfiguracaoDto(
    string BaseUrl,
    EscopoSisreg Escopo,
    string Uf,
    string Municipio,
    string CentraisReguladoras,
    TipoAutenticacaoSisreg TipoAutenticacao,
    string? Login,
    bool SenhaDefinida,
    bool TokenDefinido,
    bool Ativo,
    /// <summary>Porta do CADSUS usada na importação — ver <see cref="FonteCadastroPaciente"/>.</summary>
    FonteCadastroPaciente FonteCadastroPaciente);

/// <summary>
/// Atualização da configuração SISREG. Senha/token vazios = mantém o atual;
/// preenchidos = cifra e substitui (padrão write-only).
/// </summary>
public sealed record AtualizarSisregConfiguracaoRequest(
    string BaseUrl,
    EscopoSisreg Escopo,
    string Uf,
    string Municipio,
    string CentraisReguladoras,
    TipoAutenticacaoSisreg TipoAutenticacao,
    string? Login,
    string? Senha,
    string? Token,
    bool Ativo,
    FonteCadastroPaciente FonteCadastroPaciente = FonteCadastroPaciente.Sisreg);
