namespace SMSMais.Core.Pacientes.Enriquecimento.Dtos;

/// <summary>O que o campo da fonte faria com a ficha.</summary>
public enum SituacaoCampoFicha
{
    /// <summary>A ficha está vazia e a fonte tem o dado.</summary>
    Completar,
    /// <summary>Os dois têm, e são diferentes — quem decide é a pessoa.</summary>
    Divergente,
}

/// <summary>
/// Resultado de consultar uma fonte (CADSUS pelo SER, ou e-SUS PEC) e comparar com a ficha.
/// Só os campos que <b>diferem</b> vêm em <see cref="Campos"/>; os iguais são só contados.
/// </summary>
public sealed record ComparacaoFichaDto(
    /// <summary>Chave para gravar as escolhas — os valores ficam guardados no servidor (a tela
    /// manda só QUAIS campos quer), e vencem em 20 minutos.</summary>
    Guid? ConsultaId,
    /// <summary><c>cadsus</c> ou <c>esus</c>.</summary>
    string Fonte,
    string FonteRotulo,
    bool Encontrado,
    /// <summary>Por qual documento se procurou ("CPF" ou "CNS").</summary>
    string ConsultadoPor,
    /// <summary>Quando o cadastro foi atualizado lá (o e-SUS informa; o CADSUS pelo SER, não).</summary>
    DateTime? AtualizadoNaFonteEm,
    /// <summary>O CPF de lá é outro — pode ser outra pessoa; nada se grava.</summary>
    bool Bloqueado,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<CampoComparadoDto> Campos,
    IReadOnlyList<TelefoneSugeridoDto> Telefones,
    int CamposIguais);

public sealed record CampoComparadoDto(
    /// <summary>Chave estável: cpf, cns, nome, dataNascimento, sexo, racaCor, nomeSocial,
    /// nomeMae, nomePai, email, endereco.</summary>
    string Campo,
    string Rotulo,
    string? NaFicha,
    string NaFonte,
    SituacaoCampoFicha Situacao,
    /// <summary>Nome e nascimento só são gravados se a Receita confirmar (regra de 02/10/2026).</summary>
    bool ConfereNaReceita,
    string? Observacao);

/// <summary>Número da fonte que a ficha não tem. Telefone só se ACRESCENTA — nunca troca nem apaga.</summary>
public sealed record TelefoneSugeridoDto(string Numero, string Tipo, string Rotulo);

/// <summary>
/// Consulta ao e-SUS. Sem usuário/senha, usa a conta da plataforma — só para quem tem acesso global,
/// e sem derrubar ninguém. Com usuário/senha, é a conta da própria pessoa; a senha não é guardada.
/// </summary>
public sealed record ConsultarEsusRequest(
    string? Usuario = null,
    string? Senha = null,
    /// <summary>A pessoa já está no e-SUS em outra janela e aceitou encerrar aquela sessão.</summary>
    bool EncerrarOutraSessao = false);

public sealed record AplicarEnriquecimentoRequest(
    Guid ConsultaId,
    IReadOnlyList<string>? Campos = null,
    /// <summary>Números (como vieram em <see cref="TelefoneSugeridoDto.Numero"/>) a acrescentar.</summary>
    IReadOnlyList<string>? Telefones = null);

public sealed record ResultadoEnriquecimentoDto(
    IReadOnlyList<string> Gravados,
    IReadOnlyList<string> TelefonesAcrescentados,
    /// <summary>Escolhidos que já estavam iguais quando se foi gravar (alguém mexeu no meio).</summary>
    IReadOnlyList<string> JaEstavamIguais);
