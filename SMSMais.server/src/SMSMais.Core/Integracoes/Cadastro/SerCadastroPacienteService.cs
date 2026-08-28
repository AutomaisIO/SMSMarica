using System.Globalization;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>
/// O CADSUS pela porta do SER: o painel de paciente da tela de solicitação, pesquisado por CNS ou
/// CPF (<c>form0:numeroCADSUS</c>).
///
/// <para><b>É consulta, não escrita.</b> O botão "Pesquisar" só re-renderiza dois painéis; nada é
/// gravado no SER. A trava de somente-leitura do motor continua valendo.</para>
///
/// <para><b>Por que traduzir para <see cref="ConsultaCnsRespostaDto"/>:</b> o SER devolve o
/// formulário inteiro (18 campos, incluindo endereço e telefones editáveis). Quem importa precisa
/// só da identidade, e precisa dela no mesmo shape do CADSUS do SISREG — senão a troca de fonte
/// viraria um segundo caminho de importação, com regras próprias de dedup. Endereço e telefone do
/// SER ficam de fora pelo mesmo motivo que os do CADSUS já ficavam.</para>
/// </summary>
public sealed class SerCadastroPacienteService(ISerNovaSolicitacaoService ser) : ISerCadastroPacienteService
{
    // Ids do painel do SER medidos contra a tela viva (captura de 10/08/2026). Ao contrário dos
    // telefones — que só têm `name` posicional (form0:j_id173/178) e por isso são resolvidos pelo
    // rótulo —, os campos de IDENTIDADE têm id estável. Ainda assim, o rótulo entra como segunda
    // chave: `j_id` é posicional e a SES-RJ recompila a tela sem avisar.
    private const string CampoNome = "form0:nome";
    private const string CampoCpf = "form0:cpf";
    private const string CampoCns = "form0:cns";
    private const string CampoNascimento = "form0:dataNascimento";
    private const string CampoSexo = "form0:sexo";
    private const string CampoNomeMae = "form0:nomeMae";

    public async Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(
        string cns, CancellationToken cancellationToken = default)
    {
        var digitos = SoDigitos(cns);
        if (digitos.Length != 15)
            throw new ValidacaoException("ser.cns_invalido", "CNS deve ter 15 dígitos.");

        return await ConsultarAsync(
            digitos, "Nenhum paciente encontrado no SER para este CNS.", cancellationToken);
    }

    public async Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(
        string cpf, CancellationToken cancellationToken = default)
    {
        var digitos = SoDigitos(cpf);
        if (digitos.Length != 11)
            throw new ValidacaoException("ser.cpf_invalido", "CPF deve ter 11 dígitos.");

        return await ConsultarAsync(
            digitos, "Nenhum paciente encontrado no SER para este CPF.", cancellationToken);
    }

    private async Task<ConsultaCnsRespostaDto> ConsultarAsync(
        string documento, string erroNaoEncontrado, CancellationToken cancellationToken)
    {
        var achado = await ser.PesquisarPacienteAsync(documento, cancellationToken);

        // "Não encontrado" no SER é painel VAZIO, não erro: ele simplesmente não renderiza o
        // cadastro. Traduzir para NaoEncontrado (e não para uma falha genérica) é o que permite ao
        // chamador distinguir "esse cidadão não está no CADSUS" de "a fonte caiu" — a primeira não
        // se resolve tentando de novo, a segunda sim.
        if (!achado.Encontrado)
            throw new NaoEncontradoException("ser.paciente_nao_encontrado", erroNaoEncontrado);

        var nome = Texto(achado.Campos, CampoNome, "Nome") ?? string.Empty;
        var cpf = SoDigitos(Texto(achado.Campos, CampoCpf, "CPF"));
        var cns = SoDigitos(Texto(achado.Campos, CampoCns, "CNS"));

        // O painel pode vir montado sem os campos de identidade se o layout mudar. Devolver um
        // registro em branco seria pior que falhar: criaria paciente "SEM NOME" no hub.
        if (nome.Length == 0 && cpf.Length == 0 && cns.Length == 0)
        {
            throw new ValidacaoException(
                "ser.painel_sem_identidade",
                "O SER devolveu o painel do paciente sem nome, CPF nem CNS — o layout da tela mudou.");
        }

        return new ConsultaCnsRespostaDto(
            // Quando o SER não repete o CNS (pesquisa por CPF), fica o que foi perguntado.
            Cns: cns.Length == 15 ? cns : documento.Length == 15 ? documento : cns,
            Cpf: cpf.Length == 11 ? cpf : documento.Length == 11 ? documento : cpf,
            Nome: nome,
            Sexo: SexoCanonico(achado.Campos),
            DataNascimento: Data(Texto(achado.Campos, CampoNascimento, "Data de Nascimento")),
            NomeMae: Texto(achado.Campos, CampoNomeMae, "Nome da Mãe"));
    }

    /// <summary>Valor do campo pelo id; não achando, pelo rótulo (que sobrevive à recompilação).</summary>
    private static string? Texto(IReadOnlyList<SerCampoPacienteDto> campos, string id, string rotulo)
    {
        var achado = campos.FirstOrDefault(c => string.Equals(c.Campo, id, StringComparison.Ordinal))
                     ?? campos.FirstOrDefault(c => RotuloBate(c.Rotulo, rotulo));
        var valor = achado?.Valor?.Trim();
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    /// <summary>O rótulo do SER carrega o asterisco de obrigatório ("Nome *"); compara sem ele.</summary>
    private static bool RotuloBate(string? doSer, string esperado) =>
        (doSer ?? string.Empty).Replace("*", string.Empty).Trim()
            .Equals(esperado, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O sexo no vocabulário do hub ("Masculino"/"Feminino"), que é o mesmo que o CADSUS do SISREG
    /// devolve. O SER manda a INICIAL no <c>value</c> do select (F/M) e o rótulo por extenso na
    /// opção — usamos o valor, e o rótulo só se o valor vier fora do esperado.
    /// </summary>
    private static string? SexoCanonico(IReadOnlyList<SerCampoPacienteDto> campos)
    {
        var campo = campos.FirstOrDefault(c => string.Equals(c.Campo, CampoSexo, StringComparison.Ordinal))
                    ?? campos.FirstOrDefault(c => RotuloBate(c.Rotulo, "Sexo"));
        if (campo is null) return null;

        var valor = campo.Valor?.Trim();
        var rotulo = campo.Opcoes?.FirstOrDefault(o =>
            string.Equals(o.Valor, valor, StringComparison.OrdinalIgnoreCase))?.Rotulo;

        foreach (var candidato in new[] { valor, rotulo })
        {
            if (string.IsNullOrEmpty(candidato)) continue;
            if (candidato.StartsWith("F", StringComparison.OrdinalIgnoreCase)) return "Feminino";
            if (candidato.StartsWith("M", StringComparison.OrdinalIgnoreCase)) return "Masculino";
        }

        return null;
    }

    /// <summary>Nascimento em dd/MM/yyyy (o formato do painel do SER).</summary>
    private static DateOnly? Data(string? valor)
    {
        var t = (valor ?? string.Empty).Trim();
        foreach (var formato in (string[])["dd/MM/yyyy", "dd.MM.yyyy", "yyyy-MM-dd"])
        {
            if (DateOnly.TryParseExact(t, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        }
        return null;
    }

    private static string SoDigitos(string? valor) =>
        valor is null ? string.Empty : new string([.. valor.Where(char.IsDigit)]);
}

/// <summary>O CADSUS pela porta do SER. Interface própria para o roteador poder injetar as duas.</summary>
public interface ISerCadastroPacienteService
{
    Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(string cns, CancellationToken cancellationToken = default);
    Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(string cpf, CancellationToken cancellationToken = default);
}
