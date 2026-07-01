import { useState } from 'react';
import { Check, Link2, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { gerarLinkDownload } from '@/features/solicitacoes-exame/api/solicitacoesExameApi';

/**
 * Gera um link público de download (uso único) do exame completo e copia para a área
 * de transferência — para enviar ao paciente (ex.: colar no WhatsApp).
 */
export function BotaoLinkDownload({ solicitacaoId }: { solicitacaoId: string }) {
  const [carregando, setCarregando] = useState(false);
  const [copiado, setCopiado] = useState(false);

  async function gerar() {
    if (carregando) return;
    setCarregando(true);
    try {
      const r = await gerarLinkDownload(solicitacaoId);
      try {
        await navigator.clipboard.writeText(r.url);
        setCopiado(true);
        setTimeout(() => setCopiado(false), 3000);
      } catch {
        /* clipboard pode falhar sem HTTPS/permite — mostra no alerta abaixo mesmo assim */
      }
      const exp = new Date(r.expiraEm).toLocaleDateString('pt-BR');
      alert(`Link de download gerado (copiado):\n\n${r.url}\n\nUso único — válido até ${exp}.`);
    } catch {
      alert('Não foi possível gerar o link de download.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Button variante="outline" onClick={gerar} disabled={carregando}>
      {carregando ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      ) : copiado ? (
        <Check className="mr-2 h-4 w-4 text-green-600" />
      ) : (
        <Link2 className="mr-2 h-4 w-4" />
      )}
      {copiado ? 'Link copiado' : 'Gerar link de download'}
    </Button>
  );
}
