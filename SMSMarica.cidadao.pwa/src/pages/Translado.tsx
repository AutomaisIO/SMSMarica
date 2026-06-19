import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { http, extrairMensagemDeErro } from '@/lib/httpClient';
import { Botao, Tela } from '@/components/Tela';

type TransladoDetalhe = {
  id: string;
  data: string;
  destino: string;
  status: string;
  etaMinutos: number | null;
  acompanhanteConfirmado: boolean;
};

export function Translado() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [t, setT] = useState<TransladoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [confirmando, setConfirmando] = useState(false);

  useEffect(() => {
    let ativo = true;
    // TODO(FT7): endpoint a implementar — detalhe + ETA em tempo real (SignalR na fase seguinte).
    http
      .get<TransladoDetalhe>(`/auth/paciente/translados/${id}`)
      .then(({ data }) => ativo && setT(data))
      .catch((e) => ativo && setErro(extrairMensagemDeErro(e)));
    return () => {
      ativo = false;
    };
  }, [id]);

  async function confirmarAcompanhante() {
    setConfirmando(true);
    try {
      await http.post(`/auth/paciente/translados/${id}/acompanhante`, { confirmado: true });
      setT((prev) => (prev ? { ...prev, acompanhanteConfirmado: true } : prev));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setConfirmando(false);
    }
  }

  return (
    <Tela
      titulo="Detalhes do transporte"
      acao={
        <button onClick={() => navigate('/')} className="text-sm underline">
          Voltar
        </button>
      }
    >
      {erro && <p className="text-sm text-marica mb-4">{erro}</p>}
      {!erro && !t && <p className="text-neutral-500">Carregando…</p>}

      {t && (
        <div className="space-y-4">
          <div className="rounded-xl border border-neutral-200 bg-white p-4">
            <div className="text-lg font-semibold">{t.destino}</div>
            <div className="text-sm text-neutral-500">{t.data}</div>
          </div>

          <div className="rounded-xl border border-neutral-200 bg-white p-4 text-center">
            <div className="text-sm text-neutral-500">Chegada do veículo</div>
            <div className="mt-1 text-3xl font-bold text-marica">
              {t.etaMinutos == null ? '—' : `${t.etaMinutos} min`}
            </div>
            <div className="mt-1 text-xs text-neutral-400">
              Atualiza em tempo real quando o veículo estiver a caminho.
            </div>
          </div>

          <div className="rounded-xl border border-neutral-200 bg-white p-4">
            {t.acompanhanteConfirmado ? (
              <p className="text-sm text-green-700">Acompanhante confirmado.</p>
            ) : (
              <Botao onClick={confirmarAcompanhante} disabled={confirmando}>
                {confirmando ? 'Confirmando…' : 'Confirmar acompanhante'}
              </Botao>
            )}
          </div>
        </div>
      )}
    </Tela>
  );
}
