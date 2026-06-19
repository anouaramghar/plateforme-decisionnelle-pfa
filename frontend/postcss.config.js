import tailwindcss from 'tailwindcss';
import autoprefixer from 'autoprefixer';

const tailwind = tailwindcss();
const wrappedTailwind = {
  postcssPlugin: 'wrapped-tailwindcss',
  plugins: tailwind.plugins.map(p => {
    return (root, result) => {
      const file = root.source?.input?.file;
      if (file && (file.includes('node_modules') || file.includes('node_modules'))) {
        return;
      }
      return p(root, result);
    };
  })
};

export default {
  plugins: [
    wrappedTailwind,
    autoprefixer,
  ],
};
