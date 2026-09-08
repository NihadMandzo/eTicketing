// Required by @ngx-env/builder for SSR: the server bundle has no static replacement pass of its
// own, so this shim is what makes `import.meta.env.NG_APP_*` resolve during server rendering. Must
// stay the first import in the file.
import '@ngx-env/builder/runtime';

import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { config } from './app/app.config.server';

const bootstrap = (context: BootstrapContext) =>
    bootstrapApplication(App, config, context);

export default bootstrap;
