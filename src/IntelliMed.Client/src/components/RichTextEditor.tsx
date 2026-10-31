import { useEffect, useRef, useState } from 'react';
import { EditorContent, useEditor, useEditorState, type Editor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import { http, readJson } from '@/api/http';

function ToolButton({ label, icon, active, disabled, onClick }: {
  label: string; icon: string; active?: boolean; disabled?: boolean; onClick: () => void;
}) {
  return (
    <button type="button" className={`imed-rte-btn ${active ? 'is-active' : ''}`} title={label} aria-label={label}
            aria-pressed={active} disabled={disabled} onMouseDown={(e) => e.preventDefault()} onClick={onClick}>
      <i className={`bi ${icon}`} aria-hidden="true" />
    </button>
  );
}

function Toolbar({ editor, onImage, sourceMode, onToggleSource }: {
  editor: Editor; onImage: () => void; sourceMode: boolean; onToggleSource: () => void;
}) {
  const s = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      bold: e.isActive('bold'), italic: e.isActive('italic'), underline: e.isActive('underline'), strike: e.isActive('strike'),
      h1: e.isActive('heading', { level: 1 }), h2: e.isActive('heading', { level: 2 }), h3: e.isActive('heading', { level: 3 }),
      bullet: e.isActive('bulletList'), ordered: e.isActive('orderedList'), quote: e.isActive('blockquote'), link: e.isActive('link'),
      canUndo: e.can().undo(), canRedo: e.can().redo(),
    }),
  });
  const chain = () => editor.chain().focus();
  const setLink = () => {
    const previous = editor.getAttributes('link').href as string | undefined;
    const url = window.prompt('Link address (leave blank to remove the link)', previous ?? 'https://');
    if (url === null) return;
    if (!url.trim()) chain().extendMarkRange('link').unsetLink().run();
    else chain().extendMarkRange('link').setLink({ href: url.trim() }).run();
  };
  const d = sourceMode;
  return (
    <div className="imed-rte-toolbar" role="toolbar" aria-label="Formatting">
      <ToolButton label="Bold (Ctrl+B)" icon="bi-type-bold" active={s.bold} disabled={d} onClick={() => chain().toggleBold().run()} />
      <ToolButton label="Italic (Ctrl+I)" icon="bi-type-italic" active={s.italic} disabled={d} onClick={() => chain().toggleItalic().run()} />
      <ToolButton label="Underline (Ctrl+U)" icon="bi-type-underline" active={s.underline} disabled={d} onClick={() => chain().toggleUnderline().run()} />
      <ToolButton label="Strikethrough" icon="bi-type-strikethrough" active={s.strike} disabled={d} onClick={() => chain().toggleStrike().run()} />
      <span className="imed-rte-sep" />
      <ToolButton label="Heading 1" icon="bi-type-h1" active={s.h1} disabled={d} onClick={() => chain().toggleHeading({ level: 1 }).run()} />
      <ToolButton label="Heading 2" icon="bi-type-h2" active={s.h2} disabled={d} onClick={() => chain().toggleHeading({ level: 2 }).run()} />
      <ToolButton label="Heading 3" icon="bi-type-h3" active={s.h3} disabled={d} onClick={() => chain().toggleHeading({ level: 3 }).run()} />
      <span className="imed-rte-sep" />
      <ToolButton label="Bulleted list" icon="bi-list-ul" active={s.bullet} disabled={d} onClick={() => chain().toggleBulletList().run()} />
      <ToolButton label="Numbered list" icon="bi-list-ol" active={s.ordered} disabled={d} onClick={() => chain().toggleOrderedList().run()} />
      <ToolButton label="Quote" icon="bi-quote" active={s.quote} disabled={d} onClick={() => chain().toggleBlockquote().run()} />
      <span className="imed-rte-sep" />
      <ToolButton label="Insert link" icon="bi-link-45deg" active={s.link} disabled={d} onClick={setLink} />
      <ToolButton label="Insert image" icon="bi-image" disabled={d} onClick={onImage} />
      <ToolButton label="Clear formatting" icon="bi-eraser" disabled={d} onClick={() => chain().unsetAllMarks().clearNodes().run()} />
      <span className="imed-rte-sep" />
      <ToolButton label="Undo (Ctrl+Z)" icon="bi-arrow-counterclockwise" disabled={d || !s.canUndo} onClick={() => chain().undo().run()} />
      <ToolButton label="Redo (Ctrl+Y)" icon="bi-arrow-clockwise" disabled={d || !s.canRedo} onClick={() => chain().redo().run()} />
      <span className="ms-auto" />
      <ToolButton label={sourceMode ? 'Back to the formatted view' : 'Edit HTML source'} icon="bi-code-slash" active={sourceMode} onClick={onToggleSource} />
    </div>
  );
}

/**
 * The rich-text editor for email templates (replaces RadzenHtmlEditor). Images are uploaded to
 * `uploadUrl`, which returns { url } — a data: URI, so they are embedded in the saved HTML.
 */
export function RichTextEditor({ value, onChange, uploadUrl, height = 420 }: {
  value: string;
  onChange: (html: string) => void;
  uploadUrl?: string;
  height?: number;
}) {
  const fileInput = useRef<HTMLInputElement>(null);
  const [sourceMode, setSourceMode] = useState(false);
  const [uploading, setUploading] = useState(false);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const editor = useEditor({
    extensions: [StarterKit.configure({ link: { openOnClick: false } }), Image.configure({ allowBase64: true })],
    content: value,
    onUpdate: ({ editor: e }) => onChangeRef.current(e.getHTML()),
    editorProps: { attributes: { class: 'imed-rte-content', 'aria-label': 'Body', role: 'textbox', 'aria-multiline': 'true' } },
  });

  // value replaced from outside (template loaded after the editor mounted)
  useEffect(() => {
    if (editor && !editor.isFocused && value !== editor.getHTML()) editor.commands.setContent(value, { emitUpdate: false });
  }, [value, editor]);

  const upload = async (file: File) => {
    if (!uploadUrl || !editor) return;
    setUploading(true);
    try {
      const form = new FormData();
      form.append('file', file, file.name);
      const res = await http('POST', uploadUrl, form);
      const body = await readJson<{ url?: string; Url?: string }>(res);
      const src = body?.url ?? body?.Url;
      if (res.ok && src) editor.chain().focus().setImage({ src, alt: file.name }).run();
      else window.alert('The image could not be uploaded.');
    } finally {
      setUploading(false);
    }
  };

  if (!editor) return null;
  return (
    <div className={`imed-rte ${uploading ? 'is-busy' : ''}`}>
      <Toolbar editor={editor} sourceMode={sourceMode} onToggleSource={() => setSourceMode((m) => !m)}
               onImage={() => fileInput.current?.click()} />
      {sourceMode ? (
        <textarea className="imed-rte-source" style={{ height }} spellCheck={false} value={value}
                  onChange={(e) => { onChange(e.target.value); editor.commands.setContent(e.target.value, { emitUpdate: false }); }} />
      ) : (
        <EditorContent editor={editor} className="imed-rte-body" style={{ height }} />
      )}
      <input ref={fileInput} type="file" accept="image/*" hidden
             onChange={(e) => { const f = e.target.files?.[0]; if (f) upload(f); e.target.value = ''; }} />
    </div>
  );
}
