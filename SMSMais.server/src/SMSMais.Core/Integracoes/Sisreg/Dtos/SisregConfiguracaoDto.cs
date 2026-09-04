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
    /// <summary>
    /// Chave-mestra do sincronismo AUTOMÁTICO com o SISREG (varredura diária das unidades + lote de
    /// mapeamento). Só de leitura aqui: quem altera é <c>PUT /sisreg/configuracao/sincronismo-automatico</c>.
    /// </summary>
    bool SincronismoAutomaticoAtivo,
    /// <summary>Porta do CADSUS usada na importação — ver <see cref="FonteCadastroPaciente"/>.</summary>
    FonteCadastroPaciente FonteCadastroPaciente,
    /// <summary>Sessões simultâneas do SER na consulta de cadastro. 1 = uma de cada vez.</summary>
    int ConsultasSimultaneasSer);

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
    FonteCadastroPaciente FonteCadastroPaciente = FonteCadastroPaciente.Sisreg,
    int ConsultasSimultaneasSer = 1);

/// <summary>
/// Liga/desliga o sincronismo automático com o SISREG.
///
/// <para><b>Endpoint próprio, fora de <see cref="AtualizarSisregConfiguracaoRequest"/> de
/// propósito:</b> é um interruptor de emergência — precisa de um clique, não de submeter o
/// formulário inteiro de credenciais junto. Separado, desligar o sincronismo nunca arrasta uma
/// edição pela metade de URL, senha ou centrais reguladoras.</para>
/// </summary>
public sealed record AlternarSincronismoAutomaticoRequest(bool Ativo);

/// <summary>Estado após alternar, para a tela confirmar o que ficou valendo.</summary>
public sealed record SincronismoAutomaticoSisregDto(bool Ativo, string Mensagem);
