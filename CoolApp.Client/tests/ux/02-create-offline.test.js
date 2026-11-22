import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 2: a create that cannot reach the server must not hang on "Creating...".
describe('UX fix 2: creating a session without a connection', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });

  const fillInTheForm = () => {
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
    fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
    fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
    fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, []));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('says the server could not be reached, keeps the answers and lets the user try again', async () => {
    render(<StudySessions />);
    await screen.findByRole('button', { name: 'Create session' });
    fillInTheForm();
    global.fetch.mockImplementationOnce(() => Promise.reject(new TypeError('Failed to fetch')));

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(await screen.findByText(/Could not reach the server/)).toBeTruthy();
    // Not stuck on "Creating...": the button can be pressed again ...
    expect(screen.getByRole('button', { name: 'Create session' }).disabled).toBe(false);
    // ... and nothing that was typed is lost.
    expect(screen.getByLabelText('Topic').value).toBe('Testing');
  });
});
