import { useEffect, useMemo, useState } from 'react';
import { Copy, UserRoundCheck } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, hojeSP } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';
import { useListarMotoristas } from '@/features/motoristas/api/queries';
import { useListarRotas } from '@/features/translados/api/queries';
import { useGerarLiberacao } from '@/features/biometria/api/queries';
import type { LiberacaoGerada } from '@/features/biometria/types';

function horaMinuto(iso: string): string {
  return formatarInstante(iso, { hour: '2-digit', minute: '2-digit' });
}

async function copiar(texto: string) {
  try {
    await navigator.clipboard.writeText(texto);
    notificar('Código copiado.', 'sucesso');
  } catch {
    notificar('Não foi possível copiar. Leia o código na tela.', 'erro');
  }
}

/** O código gerado, grande, para a central ditar ao motorista. */
function CodigoGerado({ liberacao }: { liberacao: LiberacaoGerada }) {
  // Re-render a cada 15 s só para o aviso de "venceu" aparecer sem a pessoa mexer na tela.
  const [agora, setAgora] = useState(() => Date.now());
  useEffect(() => {
    const t = window.setInterval(() => setAgora(Date.now()), 15_000);
    return () => window.clearInterval(t);
  }, []);
  const vencido = new Date(liberacao.expiraEm).getTime() <= agora;

  return (
    <div
      className={`rounded-lg border p-4 text-center ${
        vencido ? 'border-gray-200 bg-gray-50' : 'border-amber-200 bg-amber-50'
      }`}
    >
      <p className="text-sm text-gray-700">
        Código para <strong>{liberacao.motoristaNome}</strong> entrar no veículo{' '}
        <strong>{liberacao.veiculoPlaca}</strong>
      </p>
      <p
        className={`my-3 font-mono text-5xl font-bold tracking-[0.25em] ${
          vencido ? 'text-gray-400 line-through' : 'text-gray-900'
        }`}
        aria-label={`Código ${liberacao.codigo.split('').join(' ')}`}
      >
        {liberacao.codigo}
      </p>
      {vencido ? (
        <p className="text-sm font-medium text-red-700">Este código venceu. Gere outro.</p>
      ) : (
        <p className="text-sm font-medium text-amber-900">
          Vale até {horaMinuto(liberacao.expiraEm)} e só pode ser usado uma vez.
        </p>
      )}
      <p className="mt-2 text-xs text-gray-600">
        O motorista digita em “Tenho um código da central” no tablet. Gerar outro anula este.
      </p>
      {!vencido ? (
        <button
          type="button"
          onClick={() => copiar(liberacao.codigo)}
          className="mt-3 inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-50"
        >
          <Copy className="h-3.5 w-3.5" /> Copiar código
        </button>
      ) : null}
    </div>
  );
}

function ConteudoLiberacao({ veiculoId, aoFechar }: { veiculoId: string; aoFechar: () => void }) {
  const motoristas = useListarMotoristas();
  // Quem está escalado hoje neste veículo vem primeiro (e já escolhido, se for um só). Sem a
  // permissão de Translados, a lista vem vazia e todos aparecem juntos — não atrapalha.
  const rotasHoje = useListarRotas({ veiculoId, data: hojeSP() });
  const gerar = useGerarLiberacao(veiculoId);
  const [escolhido, setEscolhido] = useState<string | null>(null);
  const [liberacao, setLiberacao] = useState<LiberacaoGerada | null>(null);

  const { escalados, outros } = useMemo(() => {
    const idsEscalados = new Set(
      (rotasHoje.data ?? []).filter((r) => r.status !== 'Cancelada').map((r) => r.motoristaId),
    );
    const ativos = (motoristas.data ?? [])
      .filter((m) => m.usuarioAtivo)
      .sort((a, b) => a.nomeCompleto.localeCompare(b.nomeCompleto, 'pt-BR'));
    return {
      escalados: ativos.filter((m) => idsEscalados.has(m.id)),
      outros: ativos.filter((m) => !idsEscalados.has(m.id)),
    };
  }, [motoristas.data, rotasHoje.data]);

  const motoristaId = escolhido ?? (escalados.length === 1 ? escalados[0].id : '');

  function gerarCodigo() {
    if (!motoristaId) return;
    gerar.mutate(motoristaId, { onSuccess: (l) => setLiberacao(l) });
  }

  if (liberacao) {
    return (
      <div className="space-y-4">
        <CodigoGerado liberacao={liberacao} />
        <div className="flex flex-wrap justify-end gap-2">
          <Button
            variante="ghost"
            onClick={() => {
              gerar.reset();
              setLiberacao(null);
            }}
          >
            Gerar outro código
          </Button>
          <Button onClick={aoFechar}>Fechar</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-700">
        Use quando o motorista não consegue entrar pelo rosto: o rosto não confere, ele não está
        escalado hoje neste veículo, a câmera falhou ou o primeiro acesso não chega no WhatsApp.
      </p>

      <Campo
        label="Motorista"
        htmlFor="liberacao-motorista"
        required
        dica={
          escalados.length > 0
            ? 'Os escalados hoje neste veículo aparecem primeiro.'
            : 'Só aparecem motoristas ativos.'
        }
      >
        <Select
          id="liberacao-motorista"
          value={motoristaId}
          onChange={(e) => {
            gerar.reset();
            setEscolhido(e.target.value);
          }}
          disabled={motoristas.isLoading || gerar.isPending}
        >
          <option value="">{motoristas.isLoading ? 'Carregando…' : 'Selecione…'}</option>
          {escalados.length > 0 ? (
            <optgroup label="Escalados hoje neste veículo">
              {escalados.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.nomeCompleto} · CPF {m.cpf}
                </option>
              ))}
            </optgroup>
          ) : null}
          {outros.length > 0 ? (
            <optgroup label={escalados.length > 0 ? 'Outros motoristas ativos' : 'Motoristas ativos'}>
              {outros.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.nomeCompleto} · CPF {m.cpf}
                </option>
              ))}
            </optgroup>
          ) : null}
        </Select>
      </Campo>

      {motoristas.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(motoristas.error)}
        </div>
      ) : null}
      {gerar.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(gerar.error)}
        </div>
      ) : null}

      <p className="text-xs text-gray-500">
        O código tem 6 números, vale 15 minutos, serve só para este veículo e este motorista e fica
        registrado com o seu nome.
      </p>

      <div className="flex justify-end gap-2">
        <Button variante="ghost" onClick={aoFechar}>
          Cancelar
        </Button>
        <Button onClick={gerarCodigo} disabled={!motoristaId || gerar.isPending}>
          {gerar.isPending ? 'Gerando…' : 'Gerar código'}
        </Button>
      </div>
    </div>
  );
}

/**
 * "Liberar entrada de motorista" (ADR-0073 §5): a central gera um código de uso único (6 números,
 * 15 min) para um motorista entrar no tablet deste veículo quando o rosto não resolve. Exige
 * Rastreamento/Edição — quem renderiza confere.
 */
export function LiberarEntradaMotorista({
  veiculoId,
  veiculoPlaca,
}: {
  veiculoId: string;
  veiculoPlaca?: string;
}) {
  const [aberto, setAberto] = useState(false);
  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
      >
        <UserRoundCheck className="h-4 w-4" /> Liberar entrada de motorista
      </button>
      <Modal
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        titulo="Liberar entrada de motorista"
        descricao={veiculoPlaca ? `Tablet do veículo ${veiculoPlaca}` : undefined}
      >
        <ConteudoLiberacao veiculoId={veiculoId} aoFechar={() => setAberto(false)} />
      </Modal>
    </>
  );
}
