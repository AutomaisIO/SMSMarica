import { useRef, useState } from 'react';
import { AlertTriangle, CheckCircle2, FileText } from 'lucide-react';
import { AppShell } from '@/components/AppShell';
import { Scanner } from '@/components/Scanner';
import { Field, GhostButton, PrimaryButton, Spinner, TextAreaField } from '@/components/ui';
import { api } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { montarPdf } from '@/lib/pdf';

type Passo = 'dados' | 'scanner' | 'enviando' | 'erro' | 'sucesso';

// Limite alinhado ao contrato do backend (até ~25 MB por documento).
const TAMANHO_MAX_BYTES = 25 * 1024 * 1024;

/**
 * Fluxo de criação de um documento: dados (nome/descrição) → scanner → monta PDF
 * → upload anônimo via token. Token é multi-uso no TTL, então ao concluir o
 * usuário pode "Enviar outro documento".
 */
export function NovoDocumento({
  token,
  aoSair,
}: {
  token: string;
  aoSair: () => void;
}) {
  const [passo, setPasso] = useState<Passo>('dados');
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [paginas, setPaginas] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  // Guarda contra duplo-envio (duplo clique em "Tentar enviar de novo"): um upload por vez.
  const enviandoRef = useRef(false);

  const nomeValido = nome.trim().length > 0;

  async function enviar(pgs: string[]) {
    if (enviandoRef.current) return;
    enviandoRef.current = true;
    setPaginas(pgs);
    setErro(null);
    setPasso('enviando');
    try {
      const pdf = await montarPdf(pgs);
      if (pdf.size > TAMANHO_MAX_BYTES) {
        setErro('O documento ficou muito grande (acima de 25 MB). Remova páginas ou refaça as fotos.');
        setPasso('erro');
        return;
      }
      await api.enviarDocumento(token, {
        arquivo: pdf,
        nome: nome.trim(),
        descricao: descricao.trim(),
        paginas: pgs.length,
      });
      setPasso('sucesso');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
      setPasso('erro');
    } finally {
      enviandoRef.current = false;
    }
  }

  function reiniciar() {
    setNome('');
    setDescricao('');
    setPaginas([]);
    setErro(null);
    setPasso('dados');
  }

  // ---- Passo: dados ------------------------------------------------------
  if (passo === 'dados') {
    return (
      <AppShell subtitulo="Novo documento" aoVoltar={aoSair}>
        <form
          className="space-y-5"
          onSubmit={(e) => {
            e.preventDefault();
            if (nomeValido) setPasso('scanner');
          }}
        >
          <div>
            <p className="font-display text-lg font-semibold text-tinta">Sobre o documento</p>
            <p className="text-sm text-tinta-mute">Dê um nome e, se quiser, descreva o exame.</p>
          </div>

          <Field
            label="Nome do documento"
            placeholder="Ex.: Mamografia 2019"
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            maxLength={200}
            autoFocus
            required
          />
          <TextAreaField
            label="Descrição (opcional)"
            placeholder="Ex.: Exame feito na clínica X, com laudo anexo."
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            maxLength={2000}
          />

          <PrimaryButton type="submit" disabled={!nomeValido}>
            Continuar
          </PrimaryButton>
        </form>
      </AppShell>
    );
  }

  // ---- Passo: scanner ----------------------------------------------------
  if (passo === 'scanner') {
    return (
      <AppShell subtitulo={nome.trim() || 'Novo documento'} aoVoltar={() => setPasso('dados')}>
        <Scanner aoConcluir={enviar} aoCancelar={() => setPasso('dados')} />
      </AppShell>
    );
  }

  // ---- Passo: enviando ---------------------------------------------------
  if (passo === 'enviando') {
    return (
      <AppShell subtitulo="Enviando">
        <div className="flex flex-col items-center gap-4 py-20 text-center">
          <Spinner className="h-8 w-8" />
          <div>
            <p className="font-display text-lg font-semibold text-tinta">Montando e enviando…</p>
            <p className="text-sm text-tinta-mute">Gerando o PDF e enviando ao profissional.</p>
          </div>
        </div>
      </AppShell>
    );
  }

  // ---- Passo: erro -------------------------------------------------------
  if (passo === 'erro') {
    return (
      <AppShell subtitulo="Não enviado" aoVoltar={() => setPasso('scanner')}>
        <div className="flex flex-col items-center gap-4 py-10 text-center">
          <span className="grid h-16 w-16 place-items-center rounded-2xl bg-marica/10 text-marica">
            <AlertTriangle className="h-8 w-8" />
          </span>
          <div>
            <p className="font-display text-lg font-semibold text-tinta">Não foi possível enviar</p>
            <p className="mt-1 max-w-xs text-sm text-tinta-mute">{erro ?? 'Tente novamente.'}</p>
          </div>
          <div className="w-full space-y-3 pt-2">
            <PrimaryButton onClick={() => void enviar(paginas)} disabled={paginas.length === 0}>
              Tentar enviar de novo
            </PrimaryButton>
            <GhostButton className="w-full" onClick={() => setPasso('scanner')}>
              Voltar às páginas
            </GhostButton>
          </div>
        </div>
      </AppShell>
    );
  }

  // ---- Passo: sucesso ----------------------------------------------------
  return (
    <AppShell subtitulo="Enviado">
      <div className="flex flex-col items-center gap-4 py-10 text-center">
        <span className="grid h-16 w-16 place-items-center rounded-2xl bg-lagoa-claro text-lagoa">
          <CheckCircle2 className="h-8 w-8" />
        </span>
        <div>
          <p className="font-display text-xl font-semibold text-tinta">Documento enviado!</p>
          <p className="mt-1 max-w-xs text-sm text-tinta-mute">
            O profissional vai revisar e anexar ao seu atendimento.
          </p>
        </div>

        <div className="mt-2 flex w-full items-center gap-3 rounded-2xl border border-areia bg-white p-3.5 text-left">
          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-marica/10 text-marica">
            <FileText className="h-5 w-5" />
          </span>
          <p className="min-w-0 flex-1 truncate text-sm font-medium text-tinta">{nome.trim()}</p>
        </div>

        <div className="w-full space-y-3 pt-2">
          <PrimaryButton onClick={reiniciar}>Enviar outro documento</PrimaryButton>
          <GhostButton className="w-full" onClick={aoSair}>
            Voltar ao início
          </GhostButton>
        </div>
      </div>
    </AppShell>
  );
}
