using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Cadastro;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Pacientes;

public sealed record PacienteResumoRegulacaoDto(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Cns,
    DateOnly? Nascimento,
    string? Sexo,
    bool CpfPendente);

public sealed record PacienteCadsusDto(
    string? Cpf,
    string? Cns,
    string Nome,
    DateOnly? Nascimento,
    string? Sexo,
    string? NomeMae,
    /// <summary>Porta por onde veio: `Sisreg` ou `Ser`. Vira `meta.source` no cadastro.</summary>
    string Fonte);

public interface IRegulacaoPacienteService
{
    Task<IReadOnlyList<PacienteResumoRegulacaoDto>> BuscarLocalAsync(string termo, CancellationToken ct);

    Task<PacienteCadsusDto> ConsultarCadsusAsync(string cpfOuCns, CancellationToken ct);

    /// <summary>Reusa o cadastro que existir; só cria quando não há nenhum.</summary>
    Task<PacienteResumoRegulacaoDto> ConfirmarCadsusAsync(PacienteCadsusDto dto, CancellationToken ct);

    Task<PacienteResumoRegulacaoDto> InformarCpfAsync(Guid pacienteId, string cpf, CancellationToken ct);
}

/// <summary>
/// Resolve <b>qual paciente</b> é o da solicitação: acha no cadastro local, consulta o CADSUS
/// quando não achar, e só então cria (plano 10).
///
/// <para><b>Não duplica a regra do cadastro.</b> Tudo o que grava paciente passa por
/// <see cref="IPacientesService"/>; o CADSUS vem sempre pelo roteador
/// <see cref="ICadastroPacienteService"/>, nunca pela porta de um sistema específico — a fonte é
/// configuração, não decisão de quem chama.</para>
///
/// <para><b>Ordem obrigatória: CNS → CPF → criar.</b> Inverter isso duplica cidadão. E cadastro
/// que já existe <b>nunca</b> tem nome, nascimento ou sexo sobrescritos pelo que o CADSUS
/// devolveu: o dado local pode ter sido corrigido por quem atendeu a pessoa.</para>
/// </summary>
public sealed class RegulacaoPacienteService(
    IPacientesService pacientes,
    ICadastroPacienteService cadastro) : IRegulacaoPacienteService
{
    public async Task<IReadOnlyList<PacienteResumoRegulacaoDto>> BuscarLocalAsync(
        string termo, CancellationToken ct)
    {
        var t = (termo ?? string.Empty).Trim();
        if (t.Length < 3) return [];

        var digitos = new string([.. t.Where(char.IsDigit)]);

        // CNS tem 15 dígitos e a busca geral não olha para ele — sem este atalho, digitar o CNS
        // de alguém já cadastrado não acha ninguém e o operador cria um duplicado.
        if (digitos.Length == 15)
        {
            var porCns = await pacientes.ObterPorCnsAsync(digitos, ct);
            if (porCns is not null) return [await ResumoAsync(porCns.Id, ct)];
        }

        var achados = await pacientes.BuscarAsync(t, ct);
        var resumos = new List<PacienteResumoRegulacaoDto>(achados.Count);
        foreach (var p in achados)
        {
            resumos.Add(new PacienteResumoRegulacaoDto(
                p.Id, p.NomeCompleto, Nulo(p.Cpf), null, p.DataNascimento, null,
                CpfPendente: string.IsNullOrWhiteSpace(p.Cpf)));
        }
        return resumos;
    }

    public async Task<PacienteCadsusDto> ConsultarCadsusAsync(string cpfOuCns, CancellationToken ct)
    {
        var digitos = new string([.. (cpfOuCns ?? string.Empty).Where(char.IsDigit)]);
        if (digitos.Length is not (11 or 15))
        {
            throw new ValidacaoException("documento", "Informe um CPF (11 dígitos) ou um CNS (15 dígitos).");
        }

        var fonte = await cadastro.FonteAtualAsync(ct);
        try
        {
            var r = digitos.Length == 11
                ? await cadastro.ConsultarPorCpfAsync(digitos, ct)
                : await cadastro.ConsultarPorCnsAsync(digitos, ct);

            return new PacienteCadsusDto(
                Nulo(r.Cpf), Nulo(r.Cns), r.Nome, r.DataNascimento, r.Sexo, r.NomeMae, fonte.ToString());
        }
        catch (NaoEncontradoException)
        {
            // Não achou é resposta, não falha: o front oferece cadastrar à mão.
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ValidacaoException)
        {
            // Citar a porta importa: "o SISREG está fora" e "o SER está fora" levam o operador a
            // ações diferentes, e a fonte é configurável.
            throw new ValidacaoException(
                "cadsus.indisponivel",
                $"Não foi possível consultar o CADSUS agora (porta {fonte}). Tente de novo em instantes.");
        }
    }

    public async Task<PacienteResumoRegulacaoDto> ConfirmarCadsusAsync(
        PacienteCadsusDto dto, CancellationToken ct)
    {
        var cns = SoDigitos(dto.Cns);
        var cpf = SoDigitos(dto.Cpf);

        // CNS primeiro: é o identificador que o CADSUS sempre traz. Procurar por CPF antes
        // deixaria passar o caso de o cadastro local existir ancorado só no CNS.
        if (cns.Length == 15)
        {
            var porCns = await pacientes.ObterPorCnsAsync(cns, ct);
            if (porCns is not null) return await ResumoAsync(porCns.Id, ct);
        }

        if (cpf.Length == 11 && CpfBr.EhValido(cpf))
        {
            var porCpf = await pacientes.ObterPorCpfAsync(cpf, ct);
            if (porCpf is not null) return await ResumoAsync(porCpf.Id, ct);
        }

        if (dto.Nascimento is null)
        {
            throw new ValidacaoException(
                "nascimento", "O CADSUS não trouxe a data de nascimento; cadastre o paciente pela tela de Pacientes.");
        }

        var id = await pacientes.CadastrarAsync(
            new CadastrarPacienteRequest(
                NomeCompleto: dto.Nome,
                // CPF sem DV válido não ancora: o paciente entra pelo CNS e a recepção informa
                // depois (ADR-0041). Gravar um CPF inválido cria duplicata que não se desfaz.
                Cpf: cpf.Length == 11 && CpfBr.EhValido(cpf) ? cpf : null,
                DataNascimento: dto.Nascimento.Value,
                Cns: cns.Length == 15 ? cns : null,
                Rg: null,
                Sexo: MapearSexo(dto.Sexo),
                NomeDaMae: dto.NomeMae),
            ct);

        return await ResumoAsync(id, ct);
    }

    public async Task<PacienteResumoRegulacaoDto> InformarCpfAsync(
        Guid pacienteId, string cpf, CancellationToken ct)
    {
        var digitos = SoDigitos(cpf);

        // A checagem de DV e a recusa de trocar CPF de cadastro existente já vivem em
        // `DefinirCpfAsync`. O que ele deliberadamente NÃO faz é olhar se o CPF é de outro
        // cadastro — isso é decisão de contexto, e aqui a decisão é oferecer o outro cadastro.
        var deOutro = await pacientes.ObterPorCpfAsync(digitos, ct);
        if (deOutro is not null && deOutro.Id != pacienteId)
        {
            // Devolve o id do outro cadastro para o front oferecer "usar este". Sem isso, o
            // operador fica sem saída e a tentação é criar um terceiro registro.
            throw new ConflitoException(
                "paciente.cpf_de_outro_cadastro",
                $"Este CPF já pertence ao cadastro de {deOutro.NomeCompleto} ({deOutro.Id}). "
                + "Use aquele cadastro em vez de repetir o CPF aqui.");
        }

        await pacientes.DefinirCpfAsync(pacienteId, digitos, ct);
        return await ResumoAsync(pacienteId, ct);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<PacienteResumoRegulacaoDto> ResumoAsync(Guid id, CancellationToken ct)
    {
        var p = await pacientes.ObterPorIdAsync(id, ct);
        return new PacienteResumoRegulacaoDto(
            p.Id, p.NomeCompleto, Nulo(p.Cpf), Nulo(p.Cns), p.DataNascimento, p.Sexo.ToString(),
            CpfPendente: string.IsNullOrWhiteSpace(p.Cpf));
    }

    private static string SoDigitos(string? v) => new([.. (v ?? string.Empty).Where(char.IsDigit)]);

    private static string? Nulo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    private static Sexo MapearSexo(string? sexo) => sexo?.Trim().ToUpperInvariant() switch
    {
        "MASCULINO" or "M" => Sexo.Masculino,
        "FEMININO" or "F" => Sexo.Feminino,
        _ => Sexo.NaoInformado,
    };
}
