using Hl7.Fhir.Model;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Pacientes.Unificacao;

using Task = System.Threading.Tasks.Task;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// Orquestração da unificação de pacientes (<see cref="PacientesService.UnificarAsync"/>): as
/// travas de segurança e a ordem das três metades (demografia no sobrevivente → <c>$merge</c> no
/// hub → repontamento de <c>smsmarica.*</c> → auditoria nos dois cadastros).
/// </summary>
public class UnificacaoPacienteTests
{
    private readonly IPacienteFhirClient _hub = Substitute.For<IPacienteFhirClient>();
    private readonly IRepontadorPacienteService _repontador = Substitute.For<IRepontadorPacienteService>();
    private readonly SMSMais.Core.Auditoria.IAuditoriaService _auditoria =
        Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>();

    private PacientesService Criar() =>
        new(_hub, _auditoria,
            Substitute.For<SMSMais.Core.Geo.IGeocodificadorService>(),
            _repontador,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PacientesService>.Instance);

    private static Patient Paciente(Guid id, string? cpf = null, string? cns = null, bool ativo = true)
    {
        var ident = new List<Identifier>();
        if (cpf is not null) ident.Add(new Identifier(PatientMergeFhir.SystemCpf, cpf));
        if (cns is not null) ident.Add(new Identifier(PatientMergeFhir.SystemCns, cns));
        return new Patient
        {
            Id = id.ToString(),
            Active = ativo,
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "FULANO DE TESTE" }],
            Identifier = ident,
        };
    }

    private void Hub(Guid id, Patient? p) =>
        _hub.ObterAsync(id, Arg.Any<CancellationToken>()).Returns(p);

    [Fact]
    public async Task Recusa_unificar_o_mesmo_cadastro()
    {
        var id = Guid.CreateVersion7();
        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(id, id)));
        Assert.Contains("paciente.unificar_mesmo", ex.Erros.Keys);
    }

    [Fact]
    public async Task Recusa_reunificar_cadastro_ja_absorvido()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        var absorvido = Paciente(a, cpf: "11111111111", ativo: false); // já inativo = já unificado
        Hub(a, absorvido);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(s, a)));
        Assert.Equal("paciente.ja_unificado", ex.Codigo);
        await _hub.DidNotReceive().FundirAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recusa_CPF_divergente_sem_confirmacao()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cpf: "22222222222"));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Criar().UnificarAsync(new UnificarPacientesRequest(s, a, ConfirmaChavesDivergentes: false)));
        Assert.Contains("paciente.chaves_divergentes", ex.Erros.Keys);
        await _hub.DidNotReceive().FundirAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repontador.DidNotReceive().RepontarAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CPF_divergente_COM_confirmacao_funde_e_reaponta()
    {
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cpf: "22222222222"));
        _hub.FundirAsync(s, a, Arg.Any<CancellationToken>())
            .Returns(new ResultadoFusaoHub(1, 2, 0, 3, 0, 0, 1));
        IReadOnlyList<ContagemRepontamento> repontou = [new ContagemRepontamento("laudo", "paciente_id", 4)];
        _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>()).Returns(repontou);

        var r = await Criar().UnificarAsync(
            new UnificarPacientesRequest(s, a, ConfirmaChavesDivergentes: true));

        Assert.Equal(4, r.ReferenciasRepontadas);
        Assert.Equal(6, r.ClinicoRepontado); // 2+3+1
        Assert.Equal(1, r.IdentificadoresAbsorvidos);
        await _hub.Received(1).FundirAsync(s, a, Arg.Any<CancellationToken>());
        await _repontador.Received(1).RepontarAsync(s, a, Arg.Any<CancellationToken>());
        // Trilha nos DOIS cadastros.
        await _auditoria.Received().RegistrarAsync(
            "Paciente", s.ToString(), "UnificacaoPaciente", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _auditoria.Received().RegistrarAsync(
            "Paciente", a.ToString(), "UnificacaoPaciente", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caso_comum_sem_CPF_no_absorvido_funde_sem_exigir_confirmacao()
    {
        // Duplicata típica de importação: o absorvido entrou só com CNS (sem CPF). Não é divergência.
        var s = Guid.CreateVersion7();
        var a = Guid.CreateVersion7();
        Hub(s, Paciente(s, cpf: "11111111111"));
        Hub(a, Paciente(a, cns: "700123456789012"));
        _hub.FundirAsync(s, a, Arg.Any<CancellationToken>())
            .Returns(new ResultadoFusaoHub(1, 0, 0, 0, 0, 0, 0));
        IReadOnlyList<ContagemRepontamento> vazio = [];
        _repontador.RepontarAsync(s, a, Arg.Any<CancellationToken>()).Returns(vazio);

        var r = await Criar().UnificarAsync(new UnificarPacientesRequest(s, a));

        Assert.Equal(0, r.ReferenciasRepontadas);
        await _hub.Received(1).FundirAsync(s, a, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Todo_alvo_do_repontador_aparece_na_previa_com_o_nome_do_modulo()
    {
        // Tabela nova no repontador sem rótulo aparece na prévia da tela pelo nome cru da tabela.
        var semRotulo = RepontadorPacienteService.Alvos
            .Select(a => a.Tabela)
            .Distinct()
            .Where(t => UnificacaoResumo.AgruparModulos([new ContagemRepontamento(t, "x", 1)])[0].Modulo == t)
            .ToList();

        Assert.Empty(semRotulo);
    }
}
