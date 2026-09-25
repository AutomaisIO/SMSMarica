using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Associacoes;
using SMSMais.Core.Associacoes.Dtos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Pacs;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.SolicitacoesExame.Identificadores;
using SMSMais.Core.Telefones;
using SMSMais.Core.Worklist;
using SMSMais.Core.Erros;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Associacoes;

/// <summary>
/// Corrida entre a associação MANUAL e o conciliador automático.
///
/// <para>Desde que a associação manual passou a reescrever o DICOM, o estudo reescrito entra no
/// PACS já com o AccessionNumber FINAL — e a varredura de 30s o reconhece. Se ela chegar primeiro,
/// grava a mesma associação que o fluxo manual ia gravar, e o INSERT do manual violava o índice
/// único (23505 → 409 "processado por outra requisição concorrente"), assustando o operador com um
/// erro apesar de o resultado estar correto. Aconteceu em produção em 11/08/2026.</para>
///
/// <para>Chegar ao mesmo destino por outro caminho é sucesso. Este caso trava isso.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AssociacaoCorridaConciliadorTests(PostgresFixture fixture)
{
    internal static ExameAssociacaoService CriarService(
        SmsMaisDbContext db, IPacsReescritorEstudoClient reescritor)
    {
        var consultaStudy = Substitute.For<IConsultaStudyClient>();
        consultaStudy.StudyExistePorStudyUidAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var solicitacoes = new SolicitacoesExameService(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            Substitute.For<IDcm4cheeMwlClient>(),
            new ResolvedorEstacaoWorklist(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance),
            new EscopoExameUnidade(db),
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(),
            Substitute.For<IPacienteResolver>(),
            Substitute.For<SMSMais.Core.Pacientes.IPacientesService>(),
            Substitute.For<IDispensaContatoService>(),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            Substitute.For<IRegistroErroService>(),
            Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            NullLogger<SolicitacoesExameService>.Instance);

        var identidades = Substitute.For<IResolvedorIdentidadeDicom>();
        identidades.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IdentidadeDicom("01074588703", "RIBEIRO^JOAO BENTO", "260804114"));

        return new ExameAssociacaoService(
            db, Substitute.For<IPacienteResolver>(), consultaStudy, solicitacoes,
            reescritor, identidades, new UsuarioAtualAccessorFake(),
            NullLogger<ExameAssociacaoService>.Instance);
    }

    [Fact]
    public async Task Conciliador_que_chega_primeiro_no_estudo_reescrito_nao_vira_erro()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var uidVoando = "1.2.392.200036.9125.2.111";
        var uidReescrito = $"2.25.{Random.Shared.NextInt64(1, long.MaxValue)}";

        // O reescritor devolve o UID novo E, no mesmo instante, o conciliador grava a associação
        // — exatamente a ordem que produziu o 409 em produção.
        var reescritor = Substitute.For<IPacsReescritorEstudoClient>();
        reescritor.ReescreverIdentidadeAsync(uidVoando, Arg.Any<IdentidadeDicom>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                db.ExameAssociacoes.Add(new ExameAssociacao
                {
                    Id = Guid.CreateVersion7(),
                    StudyInstanceUID = uidReescrito,
                    ExameImagemId = exame.Id,
                    PacienteId = Guid.NewGuid(),
                    Origem = OrigemAssociacaoExame.Automatica,
                    CriadoEm = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
                return new EstudoReescrito(uidReescrito, 4);
            });

        var dto = await CriarService(db, reescritor).AssociarAsync(
            new AssociarExameRequest(uidVoando, exame.AccessionNumber, null));

        // Sucesso, com o vínculo que o motor criou — não 409.
        Assert.Equal(uidReescrito, dto.StudyInstanceUID);

        // E UMA associação ativa só: o manual não duplicou a linha do motor.
        Assert.Equal(1, await db.ExameAssociacoes.CountAsync(
            a => a.ExameImagemId == exame.Id && a.ExcluidoEm == null));

        // A promoção foi reaplicada de qualquer forma (auto-reparo) — o exame não fica preso.
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == exame.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
    }
}
