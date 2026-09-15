using SMSMais.Core.Extensao.Dtos;

namespace SMSMais.Core.Extensao;

public interface IExtensaoCapturaService
{
    /// <summary>Grava um lote de capturas do SISREG vindo da extensão. Só inclusão.</summary>
    Task<CapturaLoteResultado> ReceberAsync(CapturaLoteRequest lote, CancellationToken ct = default);
}
