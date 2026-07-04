import { useEffect, useState } from 'react';
import { BadgeCheck, Check } from 'lucide-react';
import { api } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { usePerfil } from '@/store/perfil';
import { formatarCpf } from '@/components/AppShell';
import { AvatarUploader } from '@/components/AvatarUploader';
import { Card, ErroCard, Field, PrimaryButton, SectionHeader, Spinner } from '@/components/ui';

export function Perfil() {
  const perfil = usePerfil((s) => s.perfil);
  const carregando = usePerfil((s) => s.carregando);
  const erroPerfil = usePerfil((s) => s.erro);
  const carregar = usePerfil((s) => s.carregar);
  const setPerfil = usePerfil((s) => s.setPerfil);

  const [email, setEmail] = useState('');
  const [celular, setCelular] = useState('');
  const [residencial, setResidencial] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [salvandoFoto, setSalvandoFoto] = useState(false);
  const [ok, setOk] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!perfil && !carregando) void carregar();
  }, [perfil, carregando, carregar]);

  useEffect(() => {
    if (!perfil) return;
    setEmail(perfil.email ?? '');
    setCelular(perfil.telefoneCelular ?? perfil.telefonePrincipal ?? '');
    setResidencial(perfil.telefoneResidencial ?? '');
  }, [perfil]);

  async function trocarFoto(base64: string) {
    setSalvandoFoto(true);
    setErro(null);
    try {
      await api.salvarFoto(base64);
      if (perfil) setPerfil({ ...perfil, fotoBase64: base64 });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvandoFoto(false);
    }
  }

  async function salvar(e: React.FormEvent) {
    e.preventDefault();
    setSalvando(true);
    setErro(null);
    setOk(false);
    try {
      await api.salvarContato({
        email: email.trim() || null,
        telefonePrincipal: celular.trim() || null,
        telefoneCelular: celular.trim() || null,
        telefoneResidencial: residencial.trim() || null,
      });
      if (perfil)
        setPerfil({
          ...perfil,
          email: email.trim() || null,
          telefoneCelular: celular.trim() || null,
          telefonePrincipal: celular.trim() || null,
          telefoneResidencial: residencial.trim() || null,
        });
      setOk(true);
      setTimeout(() => setOk(false), 2500);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  if (!perfil) {
    if (erroPerfil && !carregando) {
      return (
        <div className="animate-rise pt-4">
          <SectionHeader title="Meu perfil" />
          <ErroCard mensagem={`Não foi possível carregar seu perfil. ${erroPerfil}`} aoTentar={carregar} />
        </div>
      );
    }
    return (
      <div className="grid place-items-center py-24">
        <Spinner />
      </div>
    );
  }

  const nome = perfil.nomeSocial || perfil.nome;

  return (
    <div className="animate-rise space-y-6">
      <div className="flex flex-col items-center pt-2">
        <AvatarUploader valor={perfil.fotoBase64} nome={nome} aoMudar={trocarFoto} salvando={salvandoFoto} />
        <h1 className="mt-4 text-center font-display text-2xl font-semibold text-tinta">{nome}</h1>
        <p className="text-sm text-tinta-mute">{salvandoFoto ? 'Salvando foto…' : 'Toque na câmera para trocar a foto'}</p>
      </div>

      {/* Dados oficiais (somente leitura) */}
      <Card className="overflow-hidden">
        <div className="flex items-center gap-2 border-b border-areia bg-papel/60 px-4 py-3">
          <BadgeCheck className="h-4 w-4 text-lagoa" />
          <span className="text-xs font-semibold uppercase tracking-wider text-tinta-mute">
            Cadastro oficial
          </span>
        </div>
        <dl className="divide-y divide-areia">
          <Linha rotulo="Nome" valor={perfil.nome} />
          <Linha rotulo="CPF" valor={formatarCpf(perfil.cpf)} />
          {perfil.cns && <Linha rotulo="Cartão SUS" valor={perfil.cns} />}
          {perfil.dataNascimento && <Linha rotulo="Nascimento" valor={formatarData(perfil.dataNascimento)} />}
        </dl>
      </Card>

      {/* Contatos (editáveis) */}
      <form onSubmit={salvar} className="space-y-4">
        <SectionHeader eyebrow="Como falamos com você" title="Contatos" />
        <Field
          label="E-mail"
          type="email"
          inputMode="email"
          autoComplete="email"
          placeholder="Seu e-mail"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <Field
          label="Celular / WhatsApp"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          placeholder="(21) 90000-0000"
          value={celular}
          onChange={(e) => setCelular(e.target.value)}
          hint="É neste número que enviamos seu código de acesso e avisos."
        />
        <Field
          label="Telefone fixo (opcional)"
          type="tel"
          inputMode="tel"
          placeholder="(21) 0000-0000"
          value={residencial}
          onChange={(e) => setResidencial(e.target.value)}
        />
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" carregando={salvando}>
          {ok ? (
            <>
              <Check className="h-5 w-5" /> Salvo
            </>
          ) : (
            'Salvar alterações'
          )}
        </PrimaryButton>
      </form>
    </div>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex items-center justify-between gap-4 px-4 py-3">
      <dt className="text-sm text-tinta-mute">{rotulo}</dt>
      <dd className="text-right text-sm font-medium text-tinta">{valor}</dd>
    </div>
  );
}

function formatarData(iso: string): string {
  const [a, m, d] = iso.split('-');
  return d && m && a ? `${d}/${m}/${a}` : iso;
}
