using System.Globalization;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Enriquecimento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Enriquecimento;

/// <summary>A ficha × uma fonte de fora, já decidido o que se oferece.</summary>
internal sealed record ComparacaoFicha(
    bool Bloqueado,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<CampoComparadoDto> Campos,
    IReadOnlyList<TelefoneExterno> Telefones,
    int CamposIguais);

/// <summary>
/// Compara a nossa ficha com o cadastro de uma fonte. Regra pura — sem I/O — porque é ela que decide
/// o que a tela oferece e o que o "Gravar" aceita (o Gravar recompara antes de escrever).
///
/// <para>As réguas são as que o projeto já tem: <b>CPF diferente bloqueia</b> (pode ser outra pessoa —
/// mesma guarda do CADSUS); CPF só se oferece para quem não tem; CNS diferente vira o oficial e o atual
/// fica como antigo; nome e nascimento passam pela Receita; telefone só se acrescenta. Campo vazio na
/// fonte nunca vira proposta — a fonte não apaga nada da ficha.</para>
/// </summary>
internal static class ComparadorFicha
{
    public const string CampoCpf = "cpf";
    public const string CampoCns = "cns";
    public const string CampoNome = "nome";
    public const string CampoNascimento = "dataNascimento";
    public const string CampoSexo = "sexo";
    public const string CampoRaca = "racaCor";
    public const string CampoNomeSocial = "nomeSocial";
    public const string CampoMae = "nomeMae";
    public const string CampoPai = "nomePai";
    public const string CampoEmail = "email";
    public const string CampoEndereco = "endereco";

    public static ComparacaoFicha Comparar(PacienteDto ficha, IReadOnlyCollection<string> telefonesDaFicha, FichaExterna fonte)
    {
        var rotuloFonte = FichaExterna.Rotulo(fonte.Fonte);
        var avisos = new List<string>(fonte.Avisos);
        var campos = new List<CampoComparadoDto>();
        var iguais = 0;

        void Comparar(string campo, string rotulo, string? naFicha, string? naFonte, Func<string?, string> chave,
            bool receita = false, string? obsDivergente = null)
        {
            if (string.IsNullOrWhiteSpace(naFonte)) return;
            var a = chave(naFicha);
            var b = chave(naFonte);
            if (b.Length == 0) return;
            if (a == b) { iguais++; return; }
            var situacao = a.Length == 0 ? SituacaoCampoFicha.Completar : SituacaoCampoFicha.Divergente;
            var obs = receita
                ? "Só é gravado se a Receita confirmar."
                : situacao == SituacaoCampoFicha.Divergente ? obsDivergente : null;
            campos.Add(new CampoComparadoDto(campo, rotulo, NormaFicha.Vazio(naFicha), naFonte!, situacao, receita, obs));
        }

        // ---- identidade
        var cpfFicha = NormaFicha.Digitos(ficha.Cpf);
        var cpfFonte = fonte.Cpf is { } c && CpfBr.EhValido(c) ? c : null;
        var bloqueado = false;
        if (cpfFonte is not null && cpfFicha.Length == 11 && cpfFicha != cpfFonte)
        {
            bloqueado = true;
            avisos.Insert(0,
                $"O CPF no {rotuloFonte} é {NormaFicha.FormatarCpf(cpfFonte)}, diferente do cadastro. Pode ser outra pessoa: nada desta consulta será gravado.");
        }
        else if (cpfFonte is not null && cpfFicha.Length == 0)
        {
            campos.Add(new CampoComparadoDto(CampoCpf, "CPF", null, NormaFicha.FormatarCpf(cpfFonte),
                SituacaoCampoFicha.Completar, false, "Só entra se nenhum outro cadastro tiver este CPF."));
        }
        else if (cpfFonte is not null)
        {
            iguais++;
        }

        // Sem CPF dos dois lados para conferir, a pessoa precisa saber se o nome bate antes de gravar.
        if (cpfFicha.Length == 0 && !NomesParecidos(ficha.NomeCompleto, fonte.Nome) && fonte.Nome is not null)
        {
            avisos.Add($"O nome no {rotuloFonte} (\"{fonte.Nome}\") é bem diferente do cadastro. Confira se é a mesma pessoa antes de gravar.");
        }

        Comparar(CampoCns, "CNS", Formatar(ficha.Cns, NormaFicha.FormatarCns), Formatar(fonte.Cns, NormaFicha.FormatarCns),
            NormaFicha.Digitos, obsDivergente: "O CNS de agora fica guardado como antigo — a pessoa continua sendo achada por ele.");
        Comparar(CampoNome, "Nome completo", ficha.NomeCompleto, fonte.Nome, NormaFicha.Texto, receita: true);
        Comparar(CampoNascimento, "Data de nascimento", Data(ficha.DataNascimento), Data(fonte.DataNascimento),
            v => v ?? string.Empty, receita: true);
        Comparar(CampoSexo, "Sexo", SexoTexto(ficha.Sexo), fonte.Sexo is { } s ? SexoTexto(s) : null, v => v ?? string.Empty);
        Comparar(CampoRaca, "Raça/cor", RacaTexto(ficha.RacaCor), fonte.RacaCor is { } r ? RacaTexto(r) : null, v => v ?? string.Empty);

        // ---- filiação e contato
        Comparar(CampoNomeSocial, "Nome social", ficha.NomeSocial, fonte.NomeSocial, NormaFicha.Texto);
        Comparar(CampoMae, "Nome da mãe", ficha.NomeDaMae, fonte.NomeMae, NormaFicha.Texto);
        Comparar(CampoPai, "Nome do pai", ficha.NomeDoPai, fonte.NomePai, NormaFicha.Texto);
        Comparar(CampoEmail, "E-mail", ficha.Email, fonte.Email, v => (v ?? string.Empty).Trim().ToLowerInvariant());

        if (fonte.Endereco is { } end)
        {
            var naFicha = NormaFicha.ChaveEndereco(ficha.Endereco);
            var naFonte = NormaFicha.ChaveEndereco(end);
            if (naFicha == naFonte) iguais++;
            else
            {
                campos.Add(new CampoComparadoDto(CampoEndereco, "Endereço",
                    naFicha.Length == 0 ? null : NormaFicha.FormatarEndereco(ficha.Endereco!),
                    NormaFicha.FormatarEndereco(end),
                    naFicha.Length == 0 ? SituacaoCampoFicha.Completar : SituacaoCampoFicha.Divergente,
                    false,
                    naFicha.Length == 0 ? null : "Troca o endereço inteiro (as coordenadas do mapa são recalculadas)."));
            }
        }

        // ---- telefones: o que a ficha não tem (nem no histórico, nem negado)
        var telefones = fonte.Telefones
            .Where(t => !telefonesDaFicha.Any(f => NormaFicha.MesmoNumero(f, t.Numero)))
            .ToList();

        return new ComparacaoFicha(bloqueado, avisos, campos, telefones, iguais);
    }

    /// <summary>Mesmo primeiro OU mesmo último nome — régua frouxa de propósito: só serve para avisar
    /// "é bem diferente", não para casar ninguém.</summary>
    internal static bool NomesParecidos(string? a, string? b)
    {
        var x = Tokens(a);
        var y = Tokens(b);
        if (x.Length == 0 || y.Length == 0) return true;
        return x[0] == y[0] || x[^1] == y[^1];
    }

    private static string[] Tokens(string? nome) =>
        NormaFicha.Texto(nome).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t is not ("DA" or "DE" or "DO" or "DAS" or "DOS" or "E")).ToArray();

    private static string? Formatar(string? valor, Func<string, string> formatar) =>
        NormaFicha.Digitos(valor) is { Length: > 0 } d ? formatar(d) : null;

    private static string? Data(DateOnly? d) => d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    internal static string? SexoTexto(Sexo s) => s switch
    {
        Sexo.Masculino => "Masculino",
        Sexo.Feminino => "Feminino",
        Sexo.Outro => "Outro",
        _ => null,
    };

    internal static string? RacaTexto(RacaCor r) => r switch
    {
        RacaCor.Branca => "Branca",
        RacaCor.Preta => "Preta",
        RacaCor.Parda => "Parda",
        RacaCor.Amarela => "Amarela",
        RacaCor.Indigena => "Indígena",
        _ => null,
    };
}
